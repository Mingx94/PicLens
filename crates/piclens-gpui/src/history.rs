#[derive(Debug, Default, Clone)]
pub struct FolderHistory {
    entries: Vec<String>,
    index: Option<usize>,
}

impl FolderHistory {
    pub fn current(&self) -> Option<&str> {
        self.index
            .and_then(|i| self.entries.get(i))
            .map(String::as_str)
    }

    pub fn can_back(&self) -> bool {
        self.index.map(|i| i > 0).unwrap_or(false)
    }

    pub fn can_forward(&self) -> bool {
        self.index
            .map(|i| i + 1 < self.entries.len())
            .unwrap_or(false)
    }

    pub fn push(&mut self, path: String) {
        if self.current() == Some(path.as_str()) {
            return;
        }
        if let Some(i) = self.index {
            self.entries.truncate(i + 1);
        } else {
            self.entries.clear();
        }
        self.entries.push(path);
        self.index = Some(self.entries.len() - 1);
    }

    pub fn back(&mut self) -> Option<&str> {
        let i = self.index?;
        if i == 0 {
            return None;
        }
        self.index = Some(i - 1);
        self.current()
    }

    pub fn forward(&mut self) -> Option<&str> {
        let i = self.index?;
        if i + 1 >= self.entries.len() {
            return None;
        }
        self.index = Some(i + 1);
        self.current()
    }

    /// Same step used by Alt+← / Alt+→ and `MouseButton::Navigate`.
    pub fn step(&mut self, back: bool) -> Option<&str> {
        if back {
            self.back()
        } else {
            self.forward()
        }
    }
}

#[cfg(test)]
mod tests {
    use super::FolderHistory;

    #[test]
    fn back_forward_after_push_is_the_same_state_change_for_keyboard_and_mouse() {
        let mut history = FolderHistory::default();
        history.push("/a".into());
        history.push("/b".into());
        history.push("/c".into());
        assert_eq!(history.current(), Some("/c"));

        assert_eq!(history.step(true), Some("/b"));
        assert_eq!(history.current(), Some("/b"));
        assert!(history.can_back());
        assert!(history.can_forward());

        assert_eq!(history.step(true), Some("/a"));
        assert_eq!(history.current(), Some("/a"));
        assert!(!history.can_back());

        assert_eq!(history.step(false), Some("/b"));
        assert_eq!(history.step(false), Some("/c"));
        assert_eq!(history.current(), Some("/c"));
        assert!(!history.can_forward());
    }
}
