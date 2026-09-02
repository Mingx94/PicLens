#[derive(Debug, Default, Clone, PartialEq, Eq)]
pub struct FolderHistory {
    entries: Vec<String>,
    index: Option<usize>,
}

impl FolderHistory {
    pub fn current(&self) -> Option<&str> {
        self.index
            .and_then(|index| self.entries.get(index))
            .map(String::as_str)
    }

    pub fn can_back(&self) -> bool {
        self.index.is_some_and(|index| index > 0)
    }

    pub fn can_forward(&self) -> bool {
        self.index
            .is_some_and(|index| index + 1 < self.entries.len())
    }

    pub fn push(&mut self, path: String) {
        if self.current() == Some(path.as_str()) {
            return;
        }
        if let Some(index) = self.index {
            self.entries.truncate(index + 1);
        } else {
            self.entries.clear();
        }
        self.entries.push(path);
        self.index = Some(self.entries.len() - 1);
    }

    pub fn back(&mut self) -> Option<&str> {
        let index = self.index?;
        if index == 0 {
            return None;
        }
        self.index = Some(index - 1);
        self.current()
    }

    pub fn forward(&mut self) -> Option<&str> {
        let index = self.index?;
        if index + 1 >= self.entries.len() {
            return None;
        }
        self.index = Some(index + 1);
        self.current()
    }

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
    fn back_forward_and_new_branch_keep_one_stable_cursor() {
        let mut history = FolderHistory::default();
        history.push("/a".into());
        history.push("/b".into());
        history.push("/c".into());

        assert_eq!(history.step(true), Some("/b"));
        assert!(history.can_back());
        assert!(history.can_forward());

        history.push("/d".into());
        assert_eq!(history.current(), Some("/d"));
        assert!(!history.can_forward());
        assert_eq!(history.step(true), Some("/b"));
    }
}
