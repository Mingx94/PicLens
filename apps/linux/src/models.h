#pragma once
#include <QAbstractListModel>
#include <QVariantMap>

namespace piclens {
class Rows final : public QAbstractListModel {
    Q_OBJECT
public:
    explicit Rows(const QList<QByteArray>& names, QObject* parent=nullptr);
    int rowCount(const QModelIndex& parent={}) const override { return parent.isValid()?0:int(rows.size()); }
    QVariant data(const QModelIndex& index,int role) const override;
    QHash<int,QByteArray> roleNames() const override { return roles; }
    void replace(QVariantList values);
    void change(int row,const QVariantMap& values);
    Q_INVOKABLE QVariantMap get(int row) const;
    QVariantList rows;
private:
    QHash<int,QByteArray> roles;
};
}
