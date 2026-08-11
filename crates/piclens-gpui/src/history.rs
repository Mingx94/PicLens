#[derive(Debug, Default, Clone)]
pub struct FolderHistory {
    entries: Vec<String>,
    index: Option<usize>,
}

impl FolderHistory {
    pub fn current(&self) -> Option<&str> {
        self.index.and_then(|i| self.entries.get(i)).map(String::as_str)
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
}
