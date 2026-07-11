#pragma once

#include <QString>
#include <QVector>

namespace piclens::core::path_rules {

[[nodiscard]] Qt::CaseSensitivity pathCaseSensitivity();
[[nodiscard]] QString pathKey(const QString &path);
[[nodiscard]] bool pathEquals(const QString &left, const QString &right);
[[nodiscard]] bool hasSameDirectoryAndBasenameWithoutExtension(
    const QString &left,
    const QString &right);
[[nodiscard]] bool targetNameExists(
    const QVector<QString> &existingPaths,
    const QString &candidatePath,
    const QString &sourcePath);

} // namespace piclens::core::path_rules
