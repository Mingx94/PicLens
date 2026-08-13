//! In-app drag-drop rename session (threshold, preview, target, confirm).

#[derive(Debug, Clone, PartialEq)]
pub enum DragPhase {
    Idle,
    Pressed {
        origin: (f64, f64),
        sources: Vec<String>,
    },
    Dragging {
        origin: (f64, f64),
        pointer: (f64, f64),
        sources: Vec<String>,
        target: Option<String>,
    },
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum DragFinish {
    Ignore,
    Cancel,
    Confirm {
        sources: Vec<String>,
        target: String,
    },
}

pub const DRAG_THRESHOLD: f64 = 8.0;

pub fn drag_begin(origin: (f64, f64), sources: Vec<String>) -> DragPhase {
    if sources.is_empty() {
        DragPhase::Idle
    } else {
        DragPhase::Pressed { origin, sources }
    }
}

pub fn drag_move(
    session: DragPhase,
    pointer: (f64, f64),
    hover_target: Option<String>,
) -> DragPhase {
    match session {
        DragPhase::Idle => DragPhase::Idle,
        DragPhase::Pressed { origin, sources } => {
            let dx = pointer.0 - origin.0;
            let dy = pointer.1 - origin.1;
            if (dx * dx + dy * dy).sqrt() < DRAG_THRESHOLD {
                DragPhase::Pressed { origin, sources }
            } else {
                let target = sanitize_target(&sources, hover_target);
                DragPhase::Dragging {
                    origin,
                    pointer,
                    sources,
                    target,
                }
            }
        }
        DragPhase::Dragging {
            origin, sources, ..
        } => {
            let target = sanitize_target(&sources, hover_target);
            DragPhase::Dragging {
                origin,
                pointer,
                sources,
                target,
            }
        }
    }
}

pub fn drag_finish(session: DragPhase) -> DragFinish {
    match session {
        DragPhase::Idle | DragPhase::Pressed { .. } => DragFinish::Ignore,
        DragPhase::Dragging {
            sources, target, ..
        } => match target {
            Some(target) => DragFinish::Confirm { sources, target },
            None => DragFinish::Cancel,
        },
    }
}

pub fn drag_cancel(_session: DragPhase) -> DragFinish {
    DragFinish::Cancel
}

pub fn is_dragging(session: &DragPhase) -> bool {
    matches!(session, DragPhase::Dragging { .. })
}

pub fn drag_preview_count(session: &DragPhase) -> usize {
    match session {
        DragPhase::Dragging { sources, .. } => sources.len(),
        _ => 0,
    }
}

pub fn drag_target(session: &DragPhase) -> Option<&str> {
    match session {
        DragPhase::Dragging { target, .. } => target.as_deref(),
        _ => None,
    }
}

fn sanitize_target(sources: &[String], hover: Option<String>) -> Option<String> {
    hover.filter(|path| !sources.iter().any(|source| source == path))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn stays_idle_before_threshold_and_drags_after() {
        let mut session = drag_begin((0.0, 0.0), vec!["/a.jpg".into()]);
        session = drag_move(session, (3.0, 4.0), Some("/b.jpg".into()));
        assert!(matches!(session, DragPhase::Pressed { .. }));
        session = drag_move(session, (10.0, 0.0), Some("/b.jpg".into()));
        assert_eq!(drag_target(&session), Some("/b.jpg"));
        assert_eq!(drag_preview_count(&session), 1);
    }

    #[test]
    fn cancel_and_ignore_do_not_name_files() {
        let pressed = drag_begin((0.0, 0.0), vec!["/a.jpg".into()]);
        assert_eq!(drag_finish(pressed.clone()), DragFinish::Ignore);
        assert_eq!(drag_cancel(pressed), DragFinish::Cancel);

        let dragging = drag_move(
            drag_begin((0.0, 0.0), vec!["/a.jpg".into()]),
            (20.0, 0.0),
            None,
        );
        assert_eq!(drag_finish(dragging), DragFinish::Cancel);
    }

    #[test]
    fn drop_on_other_image_confirms_sources_and_target() {
        let session = drag_move(
            drag_begin((0.0, 0.0), vec!["/a.jpg".into(), "/c.jpg".into()]),
            (20.0, 8.0),
            Some("/b.jpg".into()),
        );
        assert_eq!(
            drag_finish(session),
            DragFinish::Confirm {
                sources: vec!["/a.jpg".into(), "/c.jpg".into()],
                target: "/b.jpg".into(),
            }
        );
    }

    #[test]
    fn source_image_is_not_a_drop_target() {
        let session = drag_move(
            drag_begin((0.0, 0.0), vec!["/a.jpg".into()]),
            (20.0, 0.0),
            Some("/a.jpg".into()),
        );
        assert_eq!(drag_target(&session), None);
    }
}
