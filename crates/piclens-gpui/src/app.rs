//! Main window shell. Grow feature by feature on top of a working window.

use std::path::PathBuf;
use std::sync::Arc;

use gpui::*;
use gpui_component::button::{Button, ButtonVariants as _};
use gpui_component::{h_flex, v_flex, ActiveTheme, Disableable, Icon, IconName, Root};
use piclens_domain::{AppSettings, ListItem, ListQuery, SortDirection, SortKey, SortState};
use piclens_infra::{
    info, scan_folder, warn, JsonSettingsStore,
};

use crate::history::FolderHistory;

pub struct PicLensApp {
    settings_store: Arc<JsonSettingsStore>,
    settings: AppSettings,
    folder_path: Option<String>,
    items: Vec<ListItem>,
    history: FolderHistory,
    status: String,
}

impl PicLensApp {
    pub fn new(
        _window: &mut Window,
        cx: &mut Context<Self>,
        initial_folder: Option<String>,
    ) -> Self {
        let settings_store = Arc::new(JsonSettingsStore::new());
        let settings = settings_store.load();
        let mut app = Self {
            settings_store,
            settings: settings.clone(),
            folder_path: None,
            items: Vec::new(),
            history: FolderHistory::default(),
            status: "請選擇資料夾".into(),
        };

        let restore = initial_folder.or(settings.last_folder_path.clone());
        if let Some(path) = restore {
            if PathBuf::from(&path).is_dir() {
                app.open_folder(path, true, false, cx);
            }
        }
        app
    }

    fn persist_settings(&mut self) {
        if let Err(err) = self.settings_store.save(&self.settings) {
            warn(format!("settings save failed: {err}"));
        }
    }

    fn open_folder(
        &mut self,
        path: String,
        remember_picker: bool,
        push_history: bool,
        cx: &mut Context<Self>,
    ) {
        let query = ListQuery {
            folder_path: path.clone(),
            include_subfolders: self.settings.include_subfolders,
            sort: self.settings.sort,
        };
        match scan_folder(&query) {
            Ok(items) => {
                let count = items.len();
                self.folder_path = Some(path.clone());
                self.items = items;
                if push_history {
                    self.history.push(path.clone());
                }
                if remember_picker {
                    self.settings.last_folder_path = Some(path.clone());
                    self.persist_settings();
                }
                self.status = format!("已載入 {count} 個項目");
                info(format!("opened folder: {path}"));
            }
            Err(err) => {
                self.status = format!("無法開啟資料夾：{err}");
                warn(self.status.clone());
            }
        }
        cx.notify();
    }

    fn pick_folder(&mut self, cx: &mut Context<Self>) {
        if let Some(path) = rfd::FileDialog::new().pick_folder() {
            let path = path.to_string_lossy().replace('\\', "/");
            self.open_folder(path, true, true, cx);
        }
    }

    fn refresh(&mut self, cx: &mut Context<Self>) {
        if let Some(path) = self.folder_path.clone() {
            self.open_folder(path, false, false, cx);
        }
    }

    fn navigate_history(&mut self, back: bool, cx: &mut Context<Self>) {
        let path = if back {
            self.history.back().map(str::to_string)
        } else {
            self.history.forward().map(str::to_string)
        };
        if let Some(path) = path {
            self.open_folder(path, false, false, cx);
        }
    }

    fn toggle_include_subfolders(&mut self, cx: &mut Context<Self>) {
        self.settings.include_subfolders = !self.settings.include_subfolders;
        self.persist_settings();
        self.refresh(cx);
    }

    fn cycle_sort(&mut self, cx: &mut Context<Self>) {
        self.settings.sort = match (self.settings.sort.key, self.settings.sort.direction) {
            (SortKey::Name, SortDirection::Asc) => SortState {
                key: SortKey::Name,
                direction: SortDirection::Desc,
            },
            (SortKey::Name, SortDirection::Desc) => SortState {
                key: SortKey::ModifiedAt,
                direction: SortDirection::Asc,
            },
            (SortKey::ModifiedAt, SortDirection::Asc) => SortState {
                key: SortKey::ModifiedAt,
                direction: SortDirection::Desc,
            },
            (SortKey::ModifiedAt, SortDirection::Desc) => SortState {
                key: SortKey::Name,
                direction: SortDirection::Asc,
            },
        };
        self.persist_settings();
        self.refresh(cx);
    }

    fn sort_label(&self) -> &'static str {
        match (self.settings.sort.key, self.settings.sort.direction) {
            (SortKey::Name, SortDirection::Asc) => "名稱 ↑",
            (SortKey::Name, SortDirection::Desc) => "名稱 ↓",
            (SortKey::ModifiedAt, SortDirection::Asc) => "時間 ↑",
            (SortKey::ModifiedAt, SortDirection::Desc) => "時間 ↓",
        }
    }

}

impl Render for PicLensApp {
    fn render(&mut self, window: &mut Window, cx: &mut Context<Self>) -> impl IntoElement {
        let folder_title = self
            .folder_path
            .as_deref()
            .map(|p| {
                PathBuf::from(p)
                    .file_name()
                    .and_then(|n| n.to_str())
                    .unwrap_or(p)
                    .to_string()
            })
            .unwrap_or_else(|| "未選擇資料夾".into());

        let rows: Vec<_> = self
            .items
            .iter()
            .enumerate()
            .map(|(idx, item)| {
                let path = item.path().to_string();
                let name = item.name().to_string();
                let is_folder = item.is_folder();
                h_flex()
                    .id(("row", idx))
                    .w_full()
                    .gap_2()
                    .p_2()
                    .items_center()
                    .border_b_1()
                    .border_color(cx.theme().border)
                    .hover(|s| s.bg(cx.theme().secondary))
                    .cursor_pointer()
                    .child(if is_folder {
                        Icon::new(IconName::Folder)
                    } else {
                        Icon::new(IconName::File)
                    })
                    .child(div().flex_1().child(name))
                    .on_mouse_down(
                        MouseButton::Left,
                        cx.listener(move |this, _, _, cx| {
                            if is_folder {
                                this.open_folder(path.clone(), false, true, cx);
                            } else {
                                this.status = format!("已選取：{}", path);
                                cx.notify();
                            }
                        }),
                    )
            })
            .collect();

        div()
            .id("piclens-root")
            .size_full()
            .flex()
            .flex_col()
            .bg(cx.theme().background)
            .text_color(cx.theme().foreground)
            .child(
                h_flex()
                    .w_full()
                    .gap_2()
                    .p_2()
                    .items_center()
                    .border_b_1()
                    .border_color(cx.theme().border)
                    .child(
                        Button::new("open")
                            .primary()
                            .label("選擇資料夾")
                            .on_click(cx.listener(|this, _, _, cx| this.pick_folder(cx))),
                    )
                    .child(
                        Button::new("back")
                            .label("上一頁")
                            .disabled(!self.history.can_back())
                            .on_click(cx.listener(|this, _, _, cx| {
                                this.navigate_history(true, cx)
                            })),
                    )
                    .child(
                        Button::new("forward")
                            .label("下一頁")
                            .disabled(!self.history.can_forward())
                            .on_click(cx.listener(|this, _, _, cx| {
                                this.navigate_history(false, cx)
                            })),
                    )
                    .child(
                        Button::new("refresh")
                            .label("重新整理")
                            .on_click(cx.listener(|this, _, _, cx| this.refresh(cx))),
                    )
                    .child(
                        Button::new("sort")
                            .label(self.sort_label())
                            .on_click(cx.listener(|this, _, _, cx| this.cycle_sort(cx))),
                    )
                    .child(
                        Button::new("recursive")
                            .label(if self.settings.include_subfolders {
                                "含子資料夾"
                            } else {
                                "僅目前資料夾"
                            })
                            .on_click(cx.listener(|this, _, _, cx| {
                                this.toggle_include_subfolders(cx)
                            })),
                    )
                    .child(div().flex_1())
                    .child(
                        div()
                            .text_sm()
                            .text_color(cx.theme().muted_foreground)
                            .child(folder_title),
                    ),
            )
            .child(
                div()
                    .id("gallery")
                    .flex_1()
                    .w_full()
                    .overflow_y_scroll()
                    .child(if self.items.is_empty() {
                        v_flex()
                            .size_full()
                            .items_center()
                            .justify_center()
                            .gap_3()
                            .child(div().text_lg().child(if self.folder_path.is_some() {
                                "此資料夾沒有項目"
                            } else {
                                "請選擇圖片資料夾以開始"
                            }))
                            .child(
                                Button::new("empty-open")
                                    .primary()
                                    .label("選擇資料夾")
                                    .on_click(
                                        cx.listener(|this, _, _, cx| this.pick_folder(cx)),
                                    )
                                    .into_any_element(),
                            )
                            .into_any_element()
                    } else {
                        v_flex().w_full().children(rows).into_any_element()
                    }),
            )
            .child(
                h_flex()
                    .w_full()
                    .p_2()
                    .border_t_1()
                    .border_color(cx.theme().border)
                    .justify_between()
                    .child(
                        div()
                            .text_sm()
                            .text_color(cx.theme().muted_foreground)
                            .child(self.status.clone()),
                    )
                    .child(
                        div()
                            .text_sm()
                            .text_color(cx.theme().muted_foreground)
                            .child(format!("{} 項", self.items.len())),
                    ),
            )
            .children(Root::render_dialog_layer(window, cx))
            .children(Root::render_sheet_layer(window, cx))
            .children(Root::render_notification_layer(window, cx))
    }
}
