#pragma once
#include "models.h"
#include "services.h"
#include "imaging.h"
#include "fileoperations.h"
#include <QQuickImageProvider>
#include <QThreadPool>
#include <QFutureWatcher>
#include <QTimer>
#include <QElapsedTimer>
#include <QReadWriteLock>

namespace piclens {
class ImageItem;
class ThumbProvider final : public QQuickImageProvider {
public:
    ThumbProvider():QQuickImageProvider(QQuickImageProvider::Image){}
    QImage requestImage(const QString& id,QSize* size,const QSize&) override;
    void put(const QString& key,FramePtr frame);void remove(const QString& key);void clear();
private:QReadWriteLock lock;QHash<QString,FramePtr> frames;
};
class Controller final : public QObject {
    Q_OBJECT
    Q_PROPERTY(QAbstractItemModel* library READ library CONSTANT)
    Q_PROPERTY(QAbstractItemModel* tree READ tree CONSTANT)
    Q_PROPERTY(QString folder READ folder NOTIFY changed)
    Q_PROPERTY(QString rootPath READ rootPath NOTIFY changed)
    Q_PROPERTY(QString search READ search WRITE setSearch NOTIFY changed)
    Q_PROPERTY(int sortIndex READ sortIndex WRITE setSortIndex NOTIFY changed)
    Q_PROPERTY(bool recursive READ recursive WRITE setRecursive NOTIFY changed)
    Q_PROPERTY(int thumbnailSize READ thumbnailSize WRITE setThumbnailSize NOTIFY changed)
    Q_PROPERTY(bool sidebarCollapsed READ sidebarCollapsed WRITE setSidebarCollapsed NOTIFY changed)
    Q_PROPERTY(QString status READ status NOTIFY changed)
    Q_PROPERTY(QString settingsError READ settingsError NOTIFY changed)
    Q_PROPERTY(bool busy READ busy NOTIFY changed)
    Q_PROPERTY(int count READ count NOTIFY changed)
    Q_PROPERTY(int selectionCount READ selectionCount NOTIFY changed)
    Q_PROPERTY(bool viewerOpen READ viewerOpen NOTIFY changed)
    Q_PROPERTY(QString viewerName READ viewerName NOTIFY changed)
    Q_PROPERTY(QString viewerError READ viewerError NOTIFY changed)
    Q_PROPERTY(int viewerIndex READ viewerIndex NOTIFY changed)
    Q_PROPERTY(int viewerCount READ viewerCount NOTIFY changed)
    Q_PROPERTY(double zoom READ zoom NOTIFY changed)
    Q_PROPERTY(QString confirmTitle READ confirmTitle NOTIFY changed)
    Q_PROPERTY(QString confirmText READ confirmText NOTIFY changed)
    Q_PROPERTY(bool confirmOpen READ confirmOpen NOTIFY changed)
    Q_PROPERTY(bool renameOpen READ renameOpen NOTIFY changed)
    Q_PROPERTY(QString renameStem READ renameStem WRITE setRenameStem NOTIFY changed)
    Q_PROPERTY(QVariantList results READ results NOTIFY changed)
    Q_PROPERTY(bool resultsOpen MEMBER resultsOpen_ NOTIFY changed)
    Q_PROPERTY(QString toastText MEMBER toastText_ NOTIFY changed)
    Q_PROPERTY(bool toastOpen MEMBER toastOpen_ NOTIFY changed)
    Q_PROPERTY(bool toastError MEMBER toastError_ NOTIFY changed)
    Q_PROPERTY(bool dark MEMBER dark_ NOTIFY changed)
public:
    Controller(QString profile,QString worker,ThumbProvider* provider,QObject* parent=nullptr);
    ~Controller() override;
    QAbstractItemModel* library(){return &library_;} QAbstractItemModel* tree(){return &tree_;}
    QString folder()const{return folder_;} QString rootPath()const{return root_;} QString search()const{return search_;}
    int sortIndex()const{return settings_.sortKey*2+settings_.sortDirection;} bool recursive()const{return settings_.includeSubfolders;}
    int thumbnailSize()const{return settings_.thumbnailSize;} bool sidebarCollapsed()const{return settings_.sidebarCollapsed;}
    QString status()const{return status_;} QString settingsError()const{return settingsError_;} bool busy()const{return batchBusy_;}
    int count()const{return int(projection_.size());} int selectionCount()const{return int(selection_.ordered().size());}
    bool viewerOpen()const{return viewerOpen_;} QString viewerName()const;QString viewerError()const{return viewerError_;}
    int viewerIndex()const{return viewerIndex_;} int viewerCount()const{return int(snapshot_.size());} double zoom()const;
    QString confirmTitle()const{return confirmTitle_;} QString confirmText()const{return confirmText_;}
    bool confirmOpen()const{return confirmOpen_;}bool renameOpen()const{return renameOpen_;}
    QString renameStem()const{return renameStem_;}void setRenameStem(QString text){renameStem_=text;emit changed();}
    QVariantList results()const{return results_;}
    void setSearch(QString value);void setSortIndex(int value);void setRecursive(bool value);void setThumbnailSize(int value);void setSidebarCollapsed(bool value);
    void start(QString initial);void shutdown();QJsonObject metrics()const;
    Q_INVOKABLE void pick(QUrl url);
    Q_INVOKABLE void navigate(QString path);
    Q_INVOKABLE void history(int delta);Q_INVOKABLE void refresh();
    Q_INVOKABLE void toggleTree(int row);Q_INVOKABLE void activateTree(int row);
    Q_INVOKABLE void select(int row,int modifiers,bool rightClick=false);
    Q_INVOKABLE void clearSelection();Q_INVOKABLE void moveSelection(int delta,bool extend);
    Q_INVOKABLE void openViewer(int row=-1);Q_INVOKABLE void closeViewer();Q_INVOKABLE void viewerStep(int delta);
    Q_INVOKABLE void attachItem(QObject* item);Q_INVOKABLE void zoomBy(double factor);Q_INVOKABLE void resetZoom();
    Q_INVOKABLE void reveal(QString path);
    Q_INVOKABLE void requestOperation(QString kind,QString target={});Q_INVOKABLE void confirm(bool accepted);
    Q_INVOKABLE void cancelBatch();Q_INVOKABLE void dropRename(QString target);Q_INVOKABLE void retrySettings();
    Q_INVOKABLE void setVisible(QStringList paths);Q_INVOKABLE void setDragActive(bool value){dragActive_=value;}
    Q_INVOKABLE void reportMaterialized(int count){maxMaterialized_=qMax(maxMaterialized_,count);}
    Q_INVOKABLE void clearToast(){toastOpen_=false;emit changed();}
    Q_INVOKABLE void diagnose(int count=10000);Q_INVOKABLE void exercise();
signals:void changed();void focusGallery();void scrollTo(int index);
private:
    Rows library_{{"path","name","folder","animated","selected","imageKey","error","detail"}};
    Rows tree_{{"path","name","depth","expanded","loading"}};
    QString profile_,worker_,folder_,root_,search_,status_,settingsError_;Settings settings_;Selection selection_;
    QString keyboardCursor_; // 鍵盤範圍端點，與穩定 anchor、選取順序分開保存。
    QList<Entry> source_,projection_,snapshot_;QStringList history_;int historyIndex_=-1;
    QThreadPool io_,files_;QTimer saveTimer_;bool closing_=false,scanRunning_=false,scanPending_=false,saveRunning_=false,savePending_=false,profileWritable_=true;
    quint64 generation_=0,treeGeneration_=0,serial_=0,saveRevision_=0,viewerSession_=0;
    Cancel scanCancel_,treeCancel_=std::make_shared<std::atomic_bool>(false),batchCancel_;
    QElapsedTimer lifetime_,scanClock_,viewerClock_;double scanMs_=0,searchMs_=0;int maxVisible_=0,maxMaterialized_=0;QVariantList paints_;int selectedViews_=0;
    QSet<QString> treeExpanded_,treeLoading_;QHash<QString,QStringList> treeChildren_;int treeOutstanding_=0;
    Imaging imaging_;ThumbProvider* provider_;ImageItem* imageItem_=nullptr;
    struct Request {QString path;int edge;quint64 session;};QHash<QString,Request> requests_;QHash<QString,QString> thumbTokens_,imageKeys_;QSet<QString> visible_;
    QHash<QString,FramePtr> previews_;QSet<QString> previewFailures_;QString fullToken_;bool viewerOpen_=false;int viewerIndex_=0;QString viewerError_;bool sharpRecorded_=false;
    bool batchBusy_=false,confirmOpen_=false,renameOpen_=false,resultsOpen_=false,toastOpen_=false,toastError_=false,dark_=false,dragActive_=false;
    QString confirmTitle_,confirmText_,renameStem_,toastText_,pendingKind_,pendingTarget_;QList<FilePlan> plans_;QVariantList results_;
    void log(QString message);void scheduleSave();void save();void launchScan();void projectModel();void syncSelection();
    void loadTree(QString path);void rebuildTree();void treeRows(QString path,int depth,QVariantList& rows);
    void cancelGallery();void completeImage(const QString& token,FramePtr frame,const QString& error);
    QString enqueueImage(const QString& path,int edge,int priority);void showCurrent();void requestFull();void prefetch();
    void prepareOperation(QString kind,QString target);void executePlans();void setStatus(QString text);
    void executeQuietPlans();
};
}
