#include "models.h"
namespace piclens {
Rows::Rows(const QList<QByteArray>& names,QObject* parent):QAbstractListModel(parent) {
    int n=Qt::UserRole;for(const auto& name:names) roles[++n]=name;
}
QVariant Rows::data(const QModelIndex& index,int role) const {
    if(!index.isValid()||index.row()<0||index.row()>=rows.size())return {};
    return rows[index.row()].toMap().value(QString::fromUtf8(roles.value(role)));
}
void Rows::replace(QVariantList values){beginResetModel();rows=std::move(values);endResetModel();}
void Rows::change(int row,const QVariantMap& values){
    if(row<0||row>=rows.size())return;
    auto map=rows[row].toMap();for(auto i=values.begin();i!=values.end();++i)map[i.key()]=i.value();
    rows[row]=map;emit dataChanged(index(row),index(row));
}
QVariantMap Rows::get(int row)const{return row>=0&&row<rows.size()?rows[row].toMap():QVariantMap{};}
}
