#pragma once

#include <piclens/core/library_items.h>

#include <QString>
#include <QVector>

namespace piclens::core {

struct ImageSequenceSnapshot {
    QString sourceFolderPath;
    bool includeSubfolders = false;
    SortState sort;
    QVector<ImageListItem> images;
    int currentIndex = -1;
};

struct Point {
    double x = 0;
    double y = 0;

    bool operator==(const Point &) const = default;
};

struct ZoomState {
    double zoom = 1;
    Point offset;

    bool operator==(const ZoomState &) const = default;
};

} // namespace piclens::core
