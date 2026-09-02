//! App state, frame lifecycle, event handling, and action reducer.

use std::collections::VecDeque;

use piclens_domain::{normalize_thumbnail_size, sort_items, ListQuery, SortState};

use crate::backend::{Backend, Command, Event, WorkIdentity};
use crate::model::{Action, AppModel, Loadable, SelectionState};
use crate::{theme, ui, LaunchOptions};

struct Reducer {
    model: AppModel,
    actions: VecDeque<Action>,
    commands: VecDeque<Command>,
    generation: u64,
    next_request_id: u64,
    pending_probe: Option<WorkIdentity>,
    pending_library: Option<WorkIdentity>,
    close_requested: bool,
}

impl Reducer {
    fn new(initial_folder: Option<std::path::PathBuf>) -> Self {
        Self {
            model: AppModel::new(initial_folder),
            actions: VecDeque::new(),
            commands: VecDeque::new(),
            generation: 0,
            next_request_id: 1,
            pending_probe: None,
            pending_library: None,
            close_requested: false,
        }
    }

    fn push_action(&mut self, action: Action) {
        self.actions.push_back(action);
    }

    fn reduce_actions(&mut self) -> usize {
        let mut applied = 0;
        while let Some(action) = self.actions.pop_front() {
            applied += 1;
            match action {
                Action::ChooseFolder => self.push_action(Action::ShowNotice(
                    "資料夾選擇器會在圖庫垂直切片階段接上。".into(),
                )),
                Action::RetryBackendProbe => self.push_action(Action::StartBackendProbe),
                Action::DismissStatus => self.model.notice = None,
                Action::ShowNotice(message) => self.model.notice = Some(message),
                Action::StartBackendProbe => self.start_backend_probe(),
                Action::LoadLibrary(query) => self.start_library_load(query),
                Action::ReloadLibrary => {
                    if let Some(query) = self.model.library_query.clone() {
                        self.push_action(Action::LoadLibrary(query));
                    }
                }
                Action::SetSearch(search) => {
                    self.model.search = search;
                    self.model.selection = SelectionState::default();
                    self.rebuild_visible_items();
                }
                Action::SetSort(sort) => self.set_sort(sort),
                Action::ToggleIncludeSubfolders => self.toggle_include_subfolders(),
                Action::SetThumbnailSize(size) => self.set_thumbnail_size(size),
            }
        }
        applied
    }

    fn start_backend_probe(&mut self) {
        let identity = WorkIdentity {
            generation: self.generation,
            request_id: self.next_request_id,
        };
        self.next_request_id = self.next_request_id.wrapping_add(1).max(1);
        self.pending_probe = Some(identity);
        self.model.backend = Loadable::Loading;
        self.commands.push_back(Command::Probe { identity });
    }

    fn start_library_load(&mut self, query: ListQuery) {
        self.generation = self.generation.wrapping_add(1).max(1);
        let identity = WorkIdentity {
            generation: self.generation,
            request_id: self.next_request_id,
        };
        self.next_request_id = self.next_request_id.wrapping_add(1).max(1);
        self.pending_library = Some(identity);
        self.model.current_folder = Some(query.folder_path.clone().into());
        self.model.library_query = Some(query.clone());
        self.model.library = Loadable::Loading;
        self.model.visible_items.clear();
        self.model.selection = SelectionState::default();
        self.commands
            .push_back(Command::LoadLibrary { identity, query });
    }

    fn rebuild_visible_items(&mut self) {
        let Loadable::Ready(items) = &self.model.library else {
            self.model.visible_items.clear();
            return;
        };
        let search = self.model.search.trim().to_lowercase();
        let filtered = items
            .iter()
            .filter(|item| {
                search.is_empty()
                    || item.name().to_lowercase().contains(&search)
                    || item.path().to_lowercase().contains(&search)
            })
            .cloned()
            .collect::<Vec<_>>();
        let (sort, folders_first) = self
            .model
            .library_query
            .as_ref()
            .map(|query| (query.sort, !query.include_subfolders))
            .unwrap_or_default();
        self.model.visible_items = sort_items(&filtered, sort, folders_first);
    }

    fn persist_library_settings(&mut self) {
        let (include_subfolders, sort) = self
            .model
            .library_query
            .as_ref()
            .map(|query| (query.include_subfolders, query.sort))
            .unwrap_or((false, SortState::default()));
        self.commands.push_back(Command::PersistLibrarySettings {
            include_subfolders,
            sort,
            thumbnail_size: self.model.thumbnail_size,
        });
    }

    fn set_sort(&mut self, sort: SortState) {
        if let Some(query) = &mut self.model.library_query {
            query.sort = sort;
        }
        self.model.selection = SelectionState::default();
        self.rebuild_visible_items();
        self.persist_library_settings();
    }

    fn toggle_include_subfolders(&mut self) {
        let Some(mut query) = self.model.library_query.clone() else {
            return;
        };
        query.include_subfolders = !query.include_subfolders;
        self.start_library_load(query);
        self.persist_library_settings();
    }

    fn set_thumbnail_size(&mut self, size: i32) {
        self.model.thumbnail_size = normalize_thumbnail_size(f64::from(size));
        self.persist_library_settings();
    }

    fn handle_event(&mut self, event: Event) -> bool {
        match event {
            Event::ProbeCompleted { identity, result } if self.pending_probe == Some(identity) => {
                self.pending_probe = None;
                self.model.backend = match result {
                    Ok(()) => Loadable::Ready(()),
                    Err(message) => Loadable::Failed(message),
                };
                true
            }
            Event::ProbeCompleted { .. } => false,
            Event::LibraryLoaded {
                identity, result, ..
            } if self.pending_library == Some(identity) => {
                self.pending_library = None;
                match result {
                    Ok(items) => {
                        self.model.library = Loadable::Ready(items);
                        self.rebuild_visible_items();
                    }
                    Err(message) => {
                        self.model.library = Loadable::Failed(message);
                        self.model.visible_items.clear();
                    }
                }
                true
            }
            Event::LibraryLoaded { .. } => false,
            Event::LibrarySettingsSaved { result } => {
                if let Err(message) = result {
                    self.model.notice = Some(message);
                    true
                } else {
                    false
                }
            }
            Event::SmokeDeadlineElapsed => {
                self.close_requested = true;
                true
            }
        }
    }

    fn fail_command(&mut self, command: &Command, message: String) -> bool {
        match command {
            Command::Probe { identity } => self.handle_event(Event::ProbeCompleted {
                identity: *identity,
                result: Err(message),
            }),
            Command::LoadLibrary { identity, query } => self.handle_event(Event::LibraryLoaded {
                identity: *identity,
                query: query.clone(),
                result: Err(message),
            }),
            Command::PersistLibrarySettings { .. } => {
                self.model.notice = Some(message);
                true
            }
            Command::Shutdown => false,
        }
    }
}

pub struct PicLensApp {
    reducer: Reducer,
    backend: Backend,
}

impl PicLensApp {
    pub fn new(creation: &eframe::CreationContext<'_>, options: LaunchOptions) -> Self {
        piclens_infra::info("egui application state created");
        theme::install(&creation.egui_ctx);
        let LaunchOptions {
            initial_folder,
            include_subfolders,
            sort,
            thumbnail_size,
            smoke_after,
        } = options;
        let backend = Backend::spawn(creation.egui_ctx.clone(), smoke_after);
        let mut app = Self {
            reducer: Reducer::new(initial_folder),
            backend,
        };
        app.reducer.model.thumbnail_size = thumbnail_size;
        app.reducer.push_action(Action::StartBackendProbe);
        if let Some(folder) = app.reducer.model.initial_folder.clone() {
            app.reducer.push_action(Action::LoadLibrary(ListQuery {
                folder_path: folder.to_string_lossy().into_owned(),
                include_subfolders,
                sort,
            }));
        }
        app.reduce_and_dispatch(&creation.egui_ctx);
        app
    }

    fn handle_events(&mut self) -> bool {
        let mut changed = false;
        for event in self.backend.poll().collect::<Vec<_>>() {
            changed |= self.reducer.handle_event(event);
        }
        changed
    }

    fn reduce_and_dispatch(&mut self, ctx: &egui::Context) {
        let mut changed = self.reducer.reduce_actions() > 0;
        while let Some(command) = self.reducer.commands.pop_front() {
            if let Err(error) = self.backend.send(command.clone()) {
                changed |= self
                    .reducer
                    .fail_command(&command, format!("背景服務無法接收工作：{error}"));
            }
        }
        if changed {
            ctx.request_repaint();
        }
    }

    fn close_if_requested(&mut self, ctx: &egui::Context) {
        if self.reducer.close_requested {
            self.reducer.close_requested = false;
            piclens_infra::info("egui main viewport close requested");
            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
        }
    }
}

impl eframe::App for PicLensApp {
    fn logic(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        if self.handle_events() {
            ctx.request_repaint();
        }
        self.reduce_and_dispatch(ctx);
        self.close_if_requested(ctx);
    }

    fn ui(&mut self, ui: &mut egui::Ui, _frame: &mut eframe::Frame) {
        let mut frame_actions = Vec::new();
        ui::show(&self.reducer.model, ui, &mut frame_actions);
        self.reducer.actions.extend(frame_actions);
        self.reduce_and_dispatch(ui.ctx());
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn next_probe(reducer: &mut Reducer) -> WorkIdentity {
        match reducer.commands.pop_front().unwrap() {
            Command::Probe { identity } => identity,
            Command::LoadLibrary { .. } => panic!("expected probe command"),
            Command::PersistLibrarySettings { .. } => panic!("expected probe command"),
            Command::Shutdown => panic!("expected probe command"),
        }
    }

    fn query(folder: &str) -> ListQuery {
        ListQuery {
            folder_path: folder.into(),
            include_subfolders: false,
            sort: Default::default(),
        }
    }

    fn next_library_load(reducer: &mut Reducer) -> (WorkIdentity, ListQuery) {
        match reducer.commands.pop_front().unwrap() {
            Command::LoadLibrary { identity, query } => (identity, query),
            Command::Probe { .. } | Command::PersistLibrarySettings { .. } | Command::Shutdown => {
                panic!("expected library command")
            }
        }
    }

    fn next_persist(reducer: &mut Reducer) -> (bool, SortState, i32) {
        match reducer.commands.pop_front().unwrap() {
            Command::PersistLibrarySettings {
                include_subfolders,
                sort,
                thumbnail_size,
            } => (include_subfolders, sort, thumbnail_size),
            Command::Probe { .. } | Command::LoadLibrary { .. } | Command::Shutdown => {
                panic!("expected settings command")
            }
        }
    }

    #[test]
    fn reducer_processes_actions_queued_by_actions_in_order() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::ChooseFolder);

        assert_eq!(reducer.reduce_actions(), 2);
        assert_eq!(
            reducer.model.notice.as_deref(),
            Some("資料夾選擇器會在圖庫垂直切片階段接上。")
        );
        assert!(reducer.actions.is_empty());
    }

    #[test]
    fn latest_request_rejects_stale_success_and_error() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::RetryBackendProbe);
        reducer.reduce_actions();
        let first = next_probe(&mut reducer);
        reducer.push_action(Action::RetryBackendProbe);
        reducer.reduce_actions();
        let second = next_probe(&mut reducer);

        assert!(!reducer.handle_event(Event::ProbeCompleted {
            identity: first,
            result: Ok(()),
        }));
        assert_eq!(reducer.model.backend, Loadable::Loading);
        assert!(reducer.handle_event(Event::ProbeCompleted {
            identity: second,
            result: Err("測試失敗".into()),
        }));
        assert_eq!(reducer.model.backend, Loadable::Failed("測試失敗".into()));
        assert!(!reducer.handle_event(Event::ProbeCompleted {
            identity: first,
            result: Err("過期錯誤".into()),
        }));
        assert_eq!(reducer.model.backend, Loadable::Failed("測試失敗".into()));
    }

    #[test]
    fn matching_success_clears_pending_request() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::StartBackendProbe);
        reducer.reduce_actions();
        let identity = next_probe(&mut reducer);

        assert!(reducer.handle_event(Event::ProbeCompleted {
            identity,
            result: Ok(()),
        }));
        assert_eq!(reducer.model.backend, Loadable::Ready(()));
        assert_eq!(reducer.pending_probe, None);
    }

    #[test]
    fn library_load_resets_collection_and_selection_once() {
        let mut reducer = Reducer::new(None);
        reducer.model.selection.ordered_paths.push("old.png".into());
        reducer.model.selection.range_anchor = Some("old.png".into());
        reducer.push_action(Action::LoadLibrary(query("new-folder")));

        assert_eq!(reducer.reduce_actions(), 1);
        let (identity, command_query) = next_library_load(&mut reducer);
        assert_eq!(identity.generation, 1);
        assert_eq!(command_query, query("new-folder"));
        assert_eq!(reducer.model.library, Loadable::Loading);
        assert!(reducer.model.selection.ordered_paths.is_empty());
        assert_eq!(reducer.model.selection.range_anchor, None);
        assert_eq!(
            reducer.model.current_folder.as_deref(),
            Some(std::path::Path::new("new-folder"))
        );
    }

    #[test]
    fn newer_library_generation_rejects_stale_result() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::LoadLibrary(query("first")));
        reducer.reduce_actions();
        let (first, first_query) = next_library_load(&mut reducer);
        reducer.push_action(Action::LoadLibrary(query("second")));
        reducer.reduce_actions();
        let (second, second_query) = next_library_load(&mut reducer);

        assert!(!reducer.handle_event(Event::LibraryLoaded {
            identity: first,
            query: first_query,
            result: Ok(Vec::new()),
        }));
        assert_eq!(reducer.model.library, Loadable::Loading);
        assert!(reducer.handle_event(Event::LibraryLoaded {
            identity: second,
            query: second_query,
            result: Err("第二次載入失敗".into()),
        }));
        assert_eq!(
            reducer.model.library,
            Loadable::Failed("第二次載入失敗".into())
        );
    }

    #[test]
    fn reload_uses_current_query_through_follow_up_action() {
        let mut reducer = Reducer::new(None);
        reducer.model.library_query = Some(query("current"));
        reducer.push_action(Action::ReloadLibrary);

        assert_eq!(reducer.reduce_actions(), 2);
        let (_, command_query) = next_library_load(&mut reducer);
        assert_eq!(command_query, query("current"));
    }

    #[test]
    fn search_and_sort_project_loaded_items_without_rescan() {
        use piclens_domain::{ImageListItem, ListItem, SortDirection, SortKey};

        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::LoadLibrary(query("current")));
        reducer.reduce_actions();
        let (identity, loaded_query) = next_library_load(&mut reducer);
        let image = |name: &str| {
            ListItem::Image(ImageListItem {
                path: format!("current/{name}"),
                name: name.into(),
                extension: "png".into(),
                modified_at_ms: None,
                size_bytes: 1,
                is_animated: false,
            })
        };
        reducer.handle_event(Event::LibraryLoaded {
            identity,
            query: loaded_query,
            result: Ok(vec![
                image("other.png"),
                image("img10.png"),
                image("img2.png"),
            ]),
        });

        reducer.push_action(Action::SetSearch("IMG".into()));
        reducer.reduce_actions();
        assert_eq!(
            reducer
                .model
                .visible_items
                .iter()
                .map(ListItem::name)
                .collect::<Vec<_>>(),
            vec!["img2.png", "img10.png"]
        );
        assert!(reducer.commands.is_empty());

        reducer.push_action(Action::SetSort(SortState {
            key: SortKey::Name,
            direction: SortDirection::Desc,
        }));
        reducer.reduce_actions();
        assert_eq!(
            reducer
                .model
                .visible_items
                .iter()
                .map(ListItem::name)
                .collect::<Vec<_>>(),
            vec!["img10.png", "img2.png"]
        );
        let (_, saved_sort, _) = next_persist(&mut reducer);
        assert_eq!(saved_sort.direction, SortDirection::Desc);
    }

    #[test]
    fn include_subfolders_reloads_and_persists_current_query() {
        let mut reducer = Reducer::new(None);
        reducer.model.library_query = Some(query("current"));
        reducer.push_action(Action::ToggleIncludeSubfolders);

        reducer.reduce_actions();
        let (_, load_query) = next_library_load(&mut reducer);
        assert!(load_query.include_subfolders);
        let (saved_include, _, _) = next_persist(&mut reducer);
        assert!(saved_include);
    }

    #[test]
    fn thumbnail_size_is_normalized_before_persisting() {
        let mut reducer = Reducer::new(None);
        reducer.push_action(Action::SetThumbnailSize(999));

        reducer.reduce_actions();
        assert_eq!(reducer.model.thumbnail_size, 240);
        let (_, _, saved_size) = next_persist(&mut reducer);
        assert_eq!(saved_size, 240);
    }
}
