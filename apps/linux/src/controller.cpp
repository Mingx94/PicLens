#include "controller.h"
#include "imageitem.h"
#include <QtConcurrent>
#include <QDesktopServices>
#include <QDir>
#include <QFileInfo>
#include <QJsonArray>
#include <QProcess>
#include <QPointer>
#include <QDateTime>
#include <QGuiApplication>
#include <QStyleHints>
#include <stop_token>
#include <thread>

namespace piclens {
QImage ThumbProvider::requestImage(const QString& id,QSize* size,const QSize&){QReadLocker guard(&lock);auto f=frames.value(id);if(!f){if(size)*size={};return {};}if(size)*size=f->image.size();return f->image;}
void ThumbProvider::put(const QString& key,FramePtr frame){QWriteLocker guard(&lock);frames[key]=std::move(frame);}
void ThumbProvider::remove(const QString& key){QWriteLocker guard(&lock);frames.remove(key);}
void ThumbProvider::clear(){QWriteLocker guard(&lock);frames.clear();}

Controller::Controller(QString profile,QString worker,ThumbProvider* provider,QObject* parent)
    :QObject(parent),profile_(std::move(profile)),worker_(std::move(worker)),imaging_(worker_,profile_+"/Thumbnails",this),provider_(provider){
    io_.setMaxThreadCount(2);files_.setMaxThreadCount(1);lifetime_.start();
    auto initial=readProfile(profile_);profileWritable_=initial.writable;settingsError_=initial.error;
    try{settings_=Settings::fromJson(initial.settings);}catch(const std::exception& e){
        QFile f(profile_+"/piclens-settings.json");QString quarantine=f.fileName()+".corrupt."+QString::number(QDateTime::currentMSecsSinceEpoch());
        profileWritable_=f.rename(quarantine);settingsError_=QStringLiteral("設定格式錯誤：")+QString::fromUtf8(e.what());
        if(!profileWritable_)settingsError_+=QStringLiteral("；無法隔離，已停止寫入。");
    }
    dark_=QGuiApplication::styleHints()->colorScheme()==Qt::ColorScheme::Dark;
    connect(QGuiApplication::styleHints(),&QStyleHints::colorSchemeChanged,this,[this](Qt::ColorScheme scheme){dark_=scheme==Qt::ColorScheme::Dark;emit changed();});
    saveTimer_.setSingleShot(true);saveTimer_.setInterval(250);connect(&saveTimer_,&QTimer::timeout,this,&Controller::save);
    connect(&imaging_,&Imaging::completed,this,&Controller::completeImage);
    connect(&imaging_,&Imaging::quiesced,this,[this]{if(batchBusy_&&!closing_)executeQuietPlans();});
    status_=QStringLiteral("選擇資料夾，開始瀏覽圖片。");
}
Controller::~Controller(){shutdown();}
void Controller::log(QString message){auto root=profile_;if(closing_)return;(void)QtConcurrent::run(&files_,[root,message]{appendLog(root,message);});}
void Controller::setStatus(QString text){status_=std::move(text);emit changed();}
void Controller::start(QString initial){if(initial.isEmpty())initial=settings_.lastFolderPath;if(!initial.isEmpty()){root_=QFileInfo(initial).absoluteFilePath();loadTree(root_);navigate(root_);}}
void Controller::shutdown(){
    if(closing_)return;closing_=true;saveTimer_.stop();if(scanCancel_)scanCancel_->store(true);treeCancel_->store(true);if(batchCancel_)batchCancel_->store(true);
    imaging_.shutdown();io_.waitForDone();files_.waitForDone();
    if(profileWritable_&&(savePending_||saveRunning_)){auto error=writeProfile(profile_,settings_.normalize().toJson());if(!error.isEmpty())appendLog(profile_,"設定儲存失敗 "+error);}
    provider_->clear();appendLog(profile_,QStringLiteral("正常關閉；背景工作已回收"));
}
void Controller::scheduleSave(){++saveRevision_;savePending_=true;saveTimer_.start();}
void Controller::save(){
    if(closing_||saveRunning_)return;if(!profileWritable_){emit changed();return;}savePending_=false;saveRunning_=true;
    auto revision=saveRevision_;auto root=profile_;auto json=settings_.normalize().toJson();auto* watcher=new QFutureWatcher<QString>(this);
    connect(watcher,&QFutureWatcher<QString>::finished,this,[this,watcher,revision]{auto error=watcher->result();watcher->deleteLater();saveRunning_=false;if(closing_)return;
        if(revision==saveRevision_)settingsError_=error.isEmpty()?QString{}:QStringLiteral("設定未儲存：")+error;
        emit changed();if(savePending_)save();});
    watcher->setFuture(QtConcurrent::run(&io_,[root,json]{return writeProfile(root,json);}));
}
void Controller::retrySettings(){if(!profileWritable_){setStatus(QStringLiteral("請先處理無法讀取或隔離的設定檔，再重新啟動。"));return;}scheduleSave();}
void Controller::setSearch(QString value){if(value==search_||batchBusy_)return;search_=std::move(value);projectModel();}
void Controller::setSortIndex(int value){value=qBound(0,value,3);if(value==sortIndex()||batchBusy_)return;settings_.sortKey=value/2;settings_.sortDirection=value%2;scheduleSave();projectModel();}
void Controller::setRecursive(bool value){if(value==recursive()||batchBusy_)return;settings_.includeSubfolders=value;scheduleSave();refresh();}
void Controller::setThumbnailSize(int value){auto next=settings_;next.thumbnailSize=value;value=next.normalize().thumbnailSize;if(value==thumbnailSize())return;settings_.thumbnailSize=value;scheduleSave();cancelGallery();emit changed();}
void Controller::setSidebarCollapsed(bool value){if(value==sidebarCollapsed())return;settings_.sidebarCollapsed=value;scheduleSave();emit changed();}
void Controller::pick(QUrl url){if(batchBusy_)return;QString path=url.toLocalFile();if(path.isEmpty())return;
    root_=QDir::cleanPath(path);settings_.lastFolderPath=root_;scheduleSave();treeCancel_->store(true);treeCancel_=std::make_shared<std::atomic_bool>(false);++treeGeneration_;
    treeChildren_.clear();treeExpanded_.clear();treeLoading_.clear();tree_.replace({});loadTree(root_);navigate(root_);
}
void Controller::navigate(QString path){
    if(batchBusy_||path.isEmpty())return;folder_=QDir::cleanPath(path);if(historyIndex_<0||history_.value(historyIndex_)!=folder_){while(history_.size()>historyIndex_+1)history_.removeLast();history_<<folder_;historyIndex_=int(history_.size())-1;}refresh();
}
void Controller::history(int delta){int next=historyIndex_+delta;if(batchBusy_||next<0||next>=history_.size())return;historyIndex_=next;folder_=history_[next];refresh();}
void Controller::refresh(){
    if(closing_||batchBusy_||folder_.isEmpty())return;++generation_;if(scanCancel_)scanCancel_->store(true);scanPending_=true;
    selection_.clear();keyboardCursor_.clear();cancelGallery();source_.clear();projection_.clear();library_.replace({});status_=QStringLiteral("正在讀取資料夾…");emit changed();launchScan();
}
void Controller::launchScan(){
    if(scanRunning_||!scanPending_||closing_)return;scanRunning_=true;scanPending_=false;scanCancel_=std::make_shared<std::atomic_bool>(false);
    auto cancel=scanCancel_;auto generation=generation_;QString path=folder_;bool recurse=recursive();scanClock_.restart();auto* watcher=new QFutureWatcher<ScanResult>(this);
    connect(watcher,&QFutureWatcher<ScanResult>::finished,this,[this,watcher,generation,path]{auto result=watcher->result();watcher->deleteLater();scanRunning_=false;if(closing_)return;
        if(generation==generation_){scanMs_=scanClock_.elapsed();source_=std::move(result.entries);projectModel();status_=result.errors.isEmpty()?QStringLiteral("%1 個項目").arg(count()):result.errors.join('\n');for(auto& error:result.errors)log(error);log(QStringLiteral("掃描 %1 項目=%2").arg(path).arg(count()));emit changed();}launchScan();});
    watcher->setFuture(QtConcurrent::run(&io_,[path,recurse,cancel]{return scanFolder(path,recurse,cancel);}));
}
void Controller::projectModel(){
    QElapsedTimer clock;clock.start();cancelGallery();selection_.clear();keyboardCursor_.clear();projection_=project(source_,search_,settings_);QVariantList rows;
    for(const auto&e:projection_)rows<<QVariantMap{{"path",e.path},{"name",e.name},{"folder",e.folder},{"animated",e.animated},{"selected",false},{"imageKey",""},{"error",""},{"detail",e.folder?QStringLiteral("資料夾"):e.animated?QStringLiteral("動畫 · 不支援預覽"):e.extension.mid(1).toUpper()}};
    library_.replace(rows);searchMs_=clock.elapsed();emit changed();
}
void Controller::syncSelection(){for(int i=0;i<projection_.size();++i){bool selected=selection_.contains(projection_[i].path);if(library_.get(i).value("selected").toBool()!=selected)library_.change(i,{{"selected",selected}});}emit changed();}
void Controller::select(int row,int modifiers,bool rightClick){
    if(batchBusy_||row<0||row>=projection_.size())return;const auto&e=projection_[row];if(e.folder){if(!rightClick)navigate(e.path);return;}
    if(rightClick)selection_.contextSelect(e.path,imagePaths(projection_));else selection_.select(e.path,imagePaths(projection_),modifiers&Qt::ControlModifier,modifiers&Qt::ShiftModifier);keyboardCursor_=e.path;syncSelection();
}
void Controller::clearSelection(){selection_.clear();keyboardCursor_.clear();syncSelection();}
void Controller::moveSelection(int delta,bool extend){
    if(batchBusy_)return;auto paths=imagePaths(projection_);if(paths.isEmpty())return;
    int current=int(paths.indexOf(keyboardCursor_));int next=current<0?0:qBound(0,current+delta,int(paths.size())-1);
    keyboardCursor_=paths[next];selection_.select(keyboardCursor_,paths,false,extend);syncSelection();
    for(int i=0;i<projection_.size();++i)if(projection_[i].path==keyboardCursor_)emit scrollTo(i);
}
void Controller::loadTree(QString path){
    if(path.isEmpty()||treeLoading_.contains(path)||treeChildren_.contains(path)||treeOutstanding_>=32)return;
    treeLoading_.insert(path);++treeOutstanding_;auto generation=treeGeneration_;auto cancel=treeCancel_;auto* watcher=new QFutureWatcher<QPair<QStringList,QString>>(this);
    connect(watcher,&QFutureWatcher<QPair<QStringList,QString>>::finished,this,[this,watcher,path,generation]{auto result=watcher->result();watcher->deleteLater();--treeOutstanding_;if(closing_||generation!=treeGeneration_)return;
        treeLoading_.remove(path);treeChildren_[path]=result.first;if(!result.second.isEmpty()){log(result.second);setStatus(result.second);}rebuildTree();});
    watcher->setFuture(QtConcurrent::run(&io_,[path,cancel]{QString error;auto children=childFolders(path,cancel,&error);std::sort(children.begin(),children.end(),[](const auto&a,const auto&b){return naturalCompare(QFileInfo(a).fileName(),QFileInfo(b).fileName())<0;});return qMakePair(children,error);}));rebuildTree();
}
void Controller::treeRows(QString path,int depth,QVariantList& rows){if(depth>256)return;for(const auto& child:treeChildren_.value(path)){rows<<QVariantMap{{"path",child},{"name",QFileInfo(child).fileName()},{"depth",depth},{"expanded",treeExpanded_.contains(child)},{"loading",treeLoading_.contains(child)}};if(treeExpanded_.contains(child))treeRows(child,depth+1,rows);}}
void Controller::rebuildTree(){QVariantList rows;treeRows(root_,0,rows);tree_.replace(rows);emit changed();}
void Controller::toggleTree(int row){auto path=tree_.get(row).value("path").toString();if(path.isEmpty())return;if(treeExpanded_.contains(path))treeExpanded_.remove(path);else{treeExpanded_.insert(path);loadTree(path);}rebuildTree();}
void Controller::activateTree(int row){navigate(tree_.get(row).value("path").toString());}
void Controller::cancelGallery(){
    for(const auto& token:thumbTokens_){imaging_.cancel(token);requests_.remove(token);}thumbTokens_.clear();
    for(auto key:imageKeys_)provider_->remove(key);imageKeys_.clear();visible_.clear();imaging_.clearRecent();
    for(int i=0;i<library_.rowCount();++i)library_.change(i,{{"imageKey",""}});
}
QString Controller::enqueueImage(const QString& path,int edge,int priority){
    QString token=QString::number(++serial_);requests_[token]={path,edge,viewerSession_};
    if(!imaging_.enqueue(path,edge,token,priority)){requests_.remove(token);return {};}return token;
}
void Controller::setVisible(QStringList paths){
    if(closing_||viewerOpen_||batchBusy_)return;QSet<QString> next(paths.begin(),paths.end());maxVisible_=qMax(maxVisible_,int(next.size()));
    for(const auto& path:visible_)if(!next.contains(path)){
        auto token=thumbTokens_.take(path);if(!token.isEmpty()){imaging_.cancel(token);requests_.remove(token);}provider_->remove(imageKeys_.take(path));
        for(int i=0;i<projection_.size();++i)if(projection_[i].path==path)library_.change(i,{{"imageKey",""}});
    }
    visible_=next;
    for(const auto&e:projection_)if(next.contains(e.path)&&!e.folder&&!e.animated&&!thumbTokens_.contains(e.path)&&!imageKeys_.contains(e.path)){
        auto token=enqueueImage(e.path,qMin(960,thumbnailSize()*2),0);if(!token.isEmpty())thumbTokens_[e.path]=token;
    }
}
QString Controller::viewerName()const{return viewerOpen_&&viewerIndex_>=0&&viewerIndex_<snapshot_.size()?snapshot_[viewerIndex_].name:QString{};}
double Controller::zoom()const{return imageItem_?imageItem_->zoom():1.0;}
void Controller::attachItem(QObject* item){imageItem_=qobject_cast<ImageItem*>(item);if(!imageItem_)return;
    connect(imageItem_,&ImageItem::zoomChanged,this,&Controller::changed);
    connect(imageItem_,&ImageItem::framePresented,this,[this](const QString& token){if(!viewerOpen_||token!=fullToken_||sharpRecorded_)return;sharpRecorded_=true;paints_<<QVariantMap{{"path",snapshot_[viewerIndex_].path},{"milliseconds",viewerClock_.elapsed()},{"fullResolution",true}};});
}
void Controller::zoomBy(double factor){if(imageItem_)imageItem_->zoomBy(factor);}
void Controller::resetZoom(){if(imageItem_)imageItem_->reset();}
void Controller::openViewer(int row){
    if(batchBusy_)return;QString path=selection_.ordered().value(0);if(path.isEmpty()&&row>=0&&row<projection_.size())path=projection_[row].path;if(path.isEmpty())return;
    snapshot_.clear();for(const auto&e:projection_)if(!e.folder)snapshot_<<e;viewerIndex_=0;for(int i=0;i<snapshot_.size();++i)if(snapshot_[i].path==path)viewerIndex_=i;
    if(snapshot_.isEmpty())return;cancelGallery();viewerOpen_=true;++viewerSession_;previews_.clear();previewFailures_.clear();showCurrent();
}
void Controller::closeViewer(){
    if(!viewerOpen_)return;viewerOpen_=false;++viewerSession_;for(auto i=requests_.begin();i!=requests_.end();){if(i.value().edge==0||i.value().edge==1024){imaging_.cancel(i.key());i=requests_.erase(i);}else ++i;}
    previews_.clear();snapshot_.clear();fullToken_.clear();if(imageItem_)imageItem_->setFrame({});emit changed();emit focusGallery();
}
void Controller::viewerStep(int delta){if(!viewerOpen_)return;int next=viewerIndex_+delta;if(next<0||next>=snapshot_.size())return;viewerIndex_=next;showCurrent();}
void Controller::showCurrent(){
    if(!fullToken_.isEmpty()){imaging_.cancel(fullToken_);requests_.remove(fullToken_);}fullToken_.clear();viewerError_.clear();sharpRecorded_=false;viewerClock_.restart();++selectedViews_;
    if(imageItem_){imageItem_->setFrame({});imageItem_->reset();}const auto&e=snapshot_[viewerIndex_];
    QSet<QString> keep;for(int i=qMax(0,viewerIndex_-1);i<=qMin(int(snapshot_.size())-1,viewerIndex_+1);++i)keep.insert(snapshot_[i].path);
    for(auto i=previews_.begin();i!=previews_.end();)if(!keep.contains(i.key()))i=previews_.erase(i);else ++i;
    for(auto i=requests_.begin();i!=requests_.end();)if(i->edge==1024&&!keep.contains(i->path)){imaging_.cancel(i.key());i=requests_.erase(i);}else ++i;
    if(e.animated){viewerError_=QStringLiteral("動畫圖片不支援預覽。");emit changed();return;}
    if(previews_.contains(e.path)){if(imageItem_)imageItem_->setFrame(previews_[e.path]);requestFull();}
    else if(previewFailures_.contains(e.path))requestFull();
    else{bool pending=false;for(const auto&r:requests_)if(r.edge==1024&&r.path==e.path)pending=true;if(!pending)enqueueImage(e.path,1024,10);}
    log("Viewer "+e.path);emit changed();
}
void Controller::requestFull(){if(viewerOpen_&&fullToken_.isEmpty())fullToken_=enqueueImage(snapshot_[viewerIndex_].path,0,20);}
void Controller::prefetch(){if(!viewerOpen_)return;
    for(int delta:{1,-1}){int i=viewerIndex_+delta;if(i<0||i>=snapshot_.size()||snapshot_[i].animated)continue;QString path=snapshot_[i].path;if(previews_.contains(path)||previewFailures_.contains(path))continue;
        bool pending=false;for(const auto&r:requests_)if(r.edge==1024&&r.path==path)pending=true;
        if(!pending)enqueueImage(path,1024,5);return;}
}
void Controller::completeImage(const QString& token,FramePtr frame,const QString& error){
    if(!requests_.contains(token)||closing_)return;auto request=requests_.take(token);
    if(request.edge>0&&request.edge<1024){
        if(thumbTokens_.value(request.path)!=token)return;thumbTokens_.remove(request.path);if(!visible_.contains(request.path)||viewerOpen_)return;
        if(frame){provider_->put(token,frame);imageKeys_[request.path]=token;}else {imageKeys_[request.path]="failed";log("縮圖 "+request.path+" "+error);}
        for(int i=0;i<projection_.size();++i)if(projection_[i].path==request.path)library_.change(i,{{"imageKey",frame?token:QString{}},{"error",error}});return;
    }
    if(!viewerOpen_||request.session!=viewerSession_)return;bool current=snapshot_[viewerIndex_].path==request.path;
    if(request.edge==1024){if(frame)previews_[request.path]=frame;else previewFailures_.insert(request.path);if(current){if(frame&&imageItem_&&fullToken_.isEmpty())imageItem_->setFrame(frame);requestFull();}else prefetch();}
    else if(current&&token==fullToken_){if(frame&&imageItem_)imageItem_->setFrame(frame);else {viewerError_=error;log("Viewer 原圖 "+request.path+" "+error);}prefetch();}
    emit changed();
}
void Controller::reveal(QString path){if(path.isEmpty()&&viewerOpen_)path=snapshot_[viewerIndex_].path;if(path.isEmpty())return;
    if(!QDesktopServices::openUrl(QUrl::fromLocalFile(QFileInfo(path).absolutePath()))){setStatus(QStringLiteral("無法開啟檔案管理器。"));log("Reveal failed "+path);}}
void Controller::requestOperation(QString kind,QString target){
    if(batchBusy_||confirmOpen_)return;if(kind=="rename"&&target.isEmpty()){
        if(selectionCount()!=1)return;renameStem_=QFileInfo(selection_.ordered().first()).completeBaseName();renameOpen_=true;emit changed();return;}
    renameOpen_=false;prepareOperation(kind,target);
}
void Controller::dropRename(QString target){if(batchBusy_||selection_.ordered().contains(target))return;prepareOperation("drop",target);}
void Controller::prepareOperation(QString kind,QString target){
    if(batchBusy_||viewerOpen_)return;batchBusy_=true;cancelGallery();pendingKind_=kind;pendingTarget_=target;auto visible=projection_;auto selected=selection_.ordered();
    auto* watcher=new QFutureWatcher<QPair<QList<FilePlan>,QString>>(this);
    connect(watcher,&QFutureWatcher<QPair<QList<FilePlan>,QString>>::finished,this,[this,watcher,kind,visible]{auto result=watcher->result();watcher->deleteLater();if(closing_)return;batchBusy_=false;
        if(!result.second.isEmpty()){setStatus(result.second);return;}plans_=result.first;
        if(plans_.isEmpty()){setStatus(QStringLiteral("沒有符合條件的圖片。"));return;}
        bool convert=kind=="jpg"||kind=="webp";
        if(kind=="rename"||(convert&&!FilePlans::requiresConversionConfirmation(imagePaths(visible).size()))){executePlans();return;}
        confirmTitle_=kind=="drop"?QStringLiteral("確認批次重新命名"):kind=="cleanup"?QStringLiteral("確認同名清理"):kind=="trash"?QStringLiteral("確認移至回收筒"):QStringLiteral("確認轉換");
        confirmText_=QStringLiteral("共 %1 個項目。\n").arg(plans_.size());
        if(convert)confirmText_+=QStringLiteral("範圍：目前搜尋與篩選結果。保留原始檔案；已存在的目標會略過。\n");
        if(kind=="cleanup")confirmText_+=QStringLiteral("保留 JPG／JPEG 與 WebP；其他同名格式移至回收筒。\n");
        if(kind=="trash")confirmText_+=QStringLiteral("只移至系統回收筒；回收失敗不會永久刪除。\n");
        for(const auto&p:plans_)confirmText_+=p.source+(p.target.isEmpty()?QString{}:" → "+p.target)+(p.skip.isEmpty()?QString{}:" ("+p.skip+")")+'\n';
        confirmOpen_=true;emit changed();});
    watcher->setFuture(QtConcurrent::run(&files_,[kind,target,visible,selected]{try{
        QList<FilePlan> plans;if(kind=="jpg"||kind=="webp")plans=FilePlans::convert(visible,kind=="jpg"?OperationKind::Jpeg:OperationKind::Webp);
        else if(kind=="cleanup")plans=FilePlans::cleanup(visible);else if(kind=="trash")plans=FilePlans::trash(selected);
        else if(kind=="rename"&&selected.size()==1)plans<<FilePlans::rename(selected.first(),target);
        else if(kind=="drop"){auto existing=QDir(QFileInfo(target).absolutePath()).entryList(QDir::AllEntries|QDir::NoDotAndDotDot|QDir::Hidden|QDir::System);plans=FilePlans::dropRename(selected,target,existing);}
        return qMakePair(plans,QString{});}catch(const std::exception&e){return qMakePair(QList<FilePlan>{},QString::fromUtf8(e.what()));}}));
}
void Controller::confirm(bool accepted){
    if(renameOpen_){renameOpen_=false;if(accepted)requestOperation("rename",renameStem_);else emit changed();return;}
    confirmOpen_=false;if(accepted)executePlans();else{plans_.clear();emit changed();}
}
void Controller::cancelBatch(){if(batchCancel_)batchCancel_->store(true);}
void Controller::executePlans(){
    if(plans_.isEmpty())return;batchBusy_=true;cancelGallery();batchCancel_=std::make_shared<std::atomic_bool>(false);imaging_.quiesce();emit changed();
}
void Controller::executeQuietPlans(){
    auto cancel=batchCancel_;auto plans=plans_;auto worker=worker_;auto root=profile_;
    status_=QStringLiteral("正在處理 %1 個項目…").arg(plans.size());emit changed();auto* watcher=new QFutureWatcher<BatchResult>(this);
    connect(watcher,&QFutureWatcher<BatchResult>::finished,this,[this,watcher]{auto batch=watcher->result();watcher->deleteLater();batchBusy_=false;if(closing_)return;
        results_.clear();for(const auto&r:batch.items){QString status;switch(r.status){case ResultStatus::Succeeded:status="成功";break;case ResultStatus::Skipped:status="略過";break;case ResultStatus::Canceled:status="取消";break;case ResultStatus::Unknown:status="結果不確定";break;default:status="失敗";}
            results_<<QVariantMap{{"source",r.source},{"target",r.target},{"status",status},{"reason",r.message}};}
        toastText_=batch.summary();toastError_=batch.failed()>0;toastOpen_=true;plans_.clear();refresh();emit changed();});
    watcher->setFuture(QtConcurrent::run(&files_,[plans,cancel,worker,root]{
        std::stop_source stop;OperationHooks hooks;
        hooks.progress=[&](qsizetype,qsizetype,const FilePlan&){if(cancel->load())stop.request_stop();};
        hooks.log=[root](const FileResult&r){appendLog(root,"檔案操作 "+r.source+" → "+r.target+" "+r.message);};
        hooks.encode=[cancel,worker,&stop](const QString&source,const QString&temporary,OperationKind kind,std::stop_token token){
            auto canceled=[&]{if(cancel->load())stop.request_stop();return token.stop_requested();};
            if(canceled())return EncodeResult{false,QStringLiteral("已取消")};
            QProcess process;process.setProgram(worker);process.setArguments({"--encode-temp",source,temporary,kind==OperationKind::Jpeg?"jpg":"webp"});process.start();
            if(!process.waitForStarted(3000))return EncodeResult{false,canceled()?QStringLiteral("已取消"):process.errorString()};QElapsedTimer clock;clock.start();
            while(!process.waitForFinished(40)){if(canceled()||clock.elapsed()>15000){process.kill();process.waitForFinished(3000);return EncodeResult{false,canceled()?QStringLiteral("已取消"):QStringLiteral("編碼逾時")};}}
            if(canceled())return EncodeResult{false,QStringLiteral("已取消")};
            return EncodeResult{process.exitStatus()==QProcess::NormalExit&&process.exitCode()==0,QString::fromUtf8(process.readAllStandardError())};};
        // Propagate cancellation even while gio is waiting, without depending on the GUI event loop.
        std::jthread monitor([cancel,&stop](std::stop_token done){while(!done.stop_requested()){if(cancel->load()){stop.request_stop();break;}std::this_thread::sleep_for(std::chrono::milliseconds(20));}});
        return FileOperations{}.execute(plans,stop.get_token(),hooks);
    }));
}
QJsonObject Controller::metrics()const{
    int readyThumbnailCount=0;for(const auto& key:imageKeys_)if(!key.isEmpty()&&key!="failed")++readyThumbnailCount;
    return {{"schemaVersion",1},{"frontEnd","qt-quick"},{"buildProfile",
#ifdef NDEBUG
    "Release"
#else
    "Debug"
#endif
    },{"qtVersion",qVersion()},{"platformPlugin",QGuiApplication::platformName()},{"itemCount",count()},{"readyThumbnailCount",readyThumbnailCount},{"maxMaterialized",maxMaterialized_},{"maxVisible",maxVisible_},{"libraryMilliseconds",scanMs_},{"searchMilliseconds",searchMs_},{"viewerSelections",selectedViews_},{"fullPaintSamples",QJsonArray::fromVariantList(paints_)},{"unpaintedSelections",selectedViews_-paints_.size()},{"observation","scene graph submission; not compositor presentation"}};
}
void Controller::diagnose(int count){source_.clear();for(int i=0;i<count;++i){Entry e;e.path=QStringLiteral("/diagnostic/image%1.png").arg(i);e.name=QStringLiteral("image%1.png").arg(i);e.extension=".png";e.animated=true;source_<<e;}projectModel();}
void Controller::exercise(){
    setSearch("png");QTimer::singleShot(250,this,[this]{setSearch("");for(int row=0;row<projection_.size();++row)if(!projection_[row].folder){select(row,0);openViewer(row);break;}});
    for(int i=1;i<=6;++i)QTimer::singleShot(700*i,this,[this,i]{if(viewerOpen_)viewerStep(i%2?1:-1);});
}
}
