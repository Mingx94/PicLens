#pragma once

#include <piclens/app/app_controller.h>
#include <piclens/presentation/folder_tree_model.h>
#include <piclens/presentation/file_operation_controller.h>
#include <piclens/presentation/library_controller.h>
#include <piclens/presentation/library_item_model.h>
#include <piclens/presentation/thumbnail_coordinator.h>
#include <piclens/presentation/viewer_controller.h>

#include <QtQmlIntegration/qqmlintegration.h>

namespace piclens::app::qml_registration {

struct AppControllerForeign
{
    Q_GADGET
    QML_FOREIGN(piclens::app::AppController)
    QML_NAMED_ELEMENT(AppController)
    QML_UNCREATABLE("AppController is created by the application.")
};

struct LibraryControllerForeign
{
    Q_GADGET
    QML_FOREIGN(piclens::presentation::LibraryController)
    QML_NAMED_ELEMENT(LibraryController)
    QML_UNCREATABLE("Use AppController.library.")
};

struct LibraryItemModelForeign
{
    Q_GADGET
    QML_FOREIGN(piclens::presentation::LibraryItemModel)
    QML_NAMED_ELEMENT(LibraryItemModel)
    QML_UNCREATABLE("Use LibraryController.items.")
};

struct FolderTreeModelForeign
{
    Q_GADGET
    QML_FOREIGN(piclens::presentation::FolderTreeModel)
    QML_NAMED_ELEMENT(FolderTreeModel)
    QML_UNCREATABLE("Use AppController.folderTree.")
};

struct ThumbnailCoordinatorForeign
{
    Q_GADGET
    QML_FOREIGN(piclens::presentation::ThumbnailCoordinator)
    QML_NAMED_ELEMENT(ThumbnailCoordinator)
    QML_UNCREATABLE("Use AppController.thumbnails.")
};

struct FileOperationControllerForeign
{
    Q_GADGET
    QML_FOREIGN(piclens::presentation::FileOperationController)
    QML_NAMED_ELEMENT(FileOperationController)
    QML_UNCREATABLE("Use AppController.fileOperations.")
};

struct ViewerControllerForeign
{
    Q_GADGET
    QML_FOREIGN(piclens::presentation::ViewerController)
    QML_NAMED_ELEMENT(ViewerController)
    QML_UNCREATABLE("Use AppController.viewer.")
};

} // namespace piclens::app::qml_registration
