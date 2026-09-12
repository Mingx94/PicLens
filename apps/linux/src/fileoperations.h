#pragma once
#include "domain.h"
#include <functional>
#include <stop_token>

namespace piclens {
struct EncodeResult { bool succeeded = false; QString error; };
struct OperationHooks {
    // 同步呼叫，必須以可取消、有逾時的外部 worker 編碼。
    // temporary 已存在且為本次專用空檔，可截斷寫入。Jpeg=quality 100，Webp=真正無損。
    // 返回前必須回收 worker 並關閉輸出；不可提交 target 或修改 source。
    std::function<EncodeResult(const QString &source, const QString &temporary,
                               OperationKind kind, std::stop_token)> encode;
    std::function<void(qsizetype completed, qsizetype total, const FilePlan &)> progress;
    std::function<void(const FileResult &)> log;
};
struct FileOperationOptions {
    QString gioProgram = QStringLiteral("gio");
    int trashTimeoutMs = 15000;
};
class FileOperations {
public:
    explicit FileOperations(FileOperationOptions options = {});
    // 同步 API：呼叫端須在非 UI 執行緒執行；hooks 也在該執行緒呼叫。
    // 計畫須先經應用層確認；取消只停止後續項目，不回滾已完成操作。
    BatchResult execute(const QList<FilePlan> &plans, std::stop_token stop = {},
                        const OperationHooks &hooks = {}) const;
private:
    FileOperationOptions options_;
};
} // namespace piclens
