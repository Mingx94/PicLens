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
    QString key = QDir::cleanPath(QFileInfo(path).absoluteFilePath());
    if (pathCaseSensitivity() == Qt::CaseInsensitive) {
        key = key.toCaseFolded();
    }
    return key;
}

bool pathEquals(const QString &left, const QString &right)
{
    return !left.isNull()
        && !right.isNull()
        && pathKey(left) == pathKey(right);
}

bool hasLinkOrJunctionComponent(const QString &path)
{
    if (path.trimmed().isEmpty()) {
        return false;
    }

    const QFileInfo file(path);
    if (file.isSymLink() || file.isJunction()) {
        return true;
    }

    QDir directory(file.absolutePath());
    while (true) {
        const QFileInfo component(directory.absolutePath());
        if (component.isSymLink() || component.isJunction()) {
            return true;
        }
        if (directory.isRoot() || !directory.cdUp()) {
            return false;
        }
    }
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
