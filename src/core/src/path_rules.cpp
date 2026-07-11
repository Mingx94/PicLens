#include <piclens/core/path_rules.h>

#include <QDir>
#include <QFileInfo>

#include <algorithm>

namespace piclens::core::path_rules {

Qt::CaseSensitivity pathCaseSensitivity()
{
#ifdef Q_OS_WIN
    return Qt::CaseInsensitive;
#else
    return Qt::CaseSensitive;
#endif
}

QString pathKey(const QString &path)
{
    return QDir::cleanPath(QFileInfo(path).absoluteFilePath());
}

bool pathEquals(const QString &left, const QString &right)
{
    return !left.isNull()
        && !right.isNull()
        && QString::compare(pathKey(left), pathKey(right), pathCaseSensitivity()) == 0;
}

bool hasSameDirectoryAndBasenameWithoutExtension(const QString &left, const QString &right)
{
    const QFileInfo leftInfo(left);
    const QFileInfo rightInfo(right);
    return pathEquals(leftInfo.absolutePath(), rightInfo.absolutePath())
        && QString::compare(
               leftInfo.completeBaseName(),
               rightInfo.completeBaseName(),
               pathCaseSensitivity())
            == 0;
}

bool targetNameExists(
    const QVector<QString> &existingPaths,
    const QString &candidatePath,
    const QString &sourcePath)
{
    return std::any_of(existingPaths.cbegin(), existingPaths.cend(), [&](const QString &path) {
        return !pathEquals(path, sourcePath)
            && hasSameDirectoryAndBasenameWithoutExtension(path, candidatePath);
    });
}

} // namespace piclens::core::path_rules
