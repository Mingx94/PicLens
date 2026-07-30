#include <piclens/core/file_operations.h>

#include <algorithm>

namespace piclens::core {

int FileOperationBatchResult::total() const
{
    return items.size();
}

int FileOperationBatchResult::succeeded() const
{
    return static_cast<int>(std::count_if(items.cbegin(), items.cend(), [](const FileOperationResult &item) {
        return item.status == FileOperationStatus::Converted
            || item.status == FileOperationStatus::Trashed
            || item.status == FileOperationStatus::Renamed;
    }));
}

int FileOperationBatchResult::skipped() const
{
    return static_cast<int>(std::count_if(items.cbegin(), items.cend(), [](const FileOperationResult &item) {
        return item.status == FileOperationStatus::Skipped;
    }));
}

int FileOperationBatchResult::failed() const
{
    return static_cast<int>(std::count_if(items.cbegin(), items.cend(), [](const FileOperationResult &item) {
        return item.status == FileOperationStatus::Failed;
    }));
}

} // namespace piclens::core
