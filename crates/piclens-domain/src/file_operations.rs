#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FileOperationStatus {
    Converted,
    Trashed,
    Renamed,
    Skipped,
    Failed,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FileOperationResult {
    pub path: String,
    pub status: FileOperationStatus,
    pub target_path: Option<String>,
    pub reason: Option<String>,
    pub message: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct FileOperationBatchResult {
    pub items: Vec<FileOperationResult>,
}

impl FileOperationBatchResult {
    pub fn total(&self) -> usize {
        self.items.len()
    }

    pub fn succeeded(&self) -> usize {
        self.items
            .iter()
            .filter(|item| {
                matches!(
                    item.status,
                    FileOperationStatus::Converted
                        | FileOperationStatus::Trashed
                        | FileOperationStatus::Renamed
                )
            })
            .count()
    }

    pub fn skipped(&self) -> usize {
        self.items
            .iter()
            .filter(|item| item.status == FileOperationStatus::Skipped)
            .count()
    }

    pub fn failed(&self) -> usize {
        self.items
            .iter()
            .filter(|item| item.status == FileOperationStatus::Failed)
            .count()
    }
}
