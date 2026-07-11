#include <piclens/infrastructure/json_settings_store.h>

#include <piclens/core/settings_rules.h>
#include <piclens/infrastructure/app_data_paths.h>

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonDocument>
#include <QJsonObject>
#include <QSaveFile>
#include <QUuid>

#include <stdexcept>

namespace piclens::infrastructure {
namespace {

QString uniqueSiblingPath(const QString &path, const QString &kind)
{
    const QFileInfo info(path);
    const QString unique = QUuid::createUuid().toString(QUuid::WithoutBraces);
    return QDir(info.absolutePath()).filePath(
        QStringLiteral("%1.%2.%3").arg(info.fileName(), kind, unique));
}

core::SortKey sortKeyFromJson(int value)
{
    return value == static_cast<int>(core::SortKey::ModifiedAt)
        ? core::SortKey::ModifiedAt
        : core::SortKey::Name;
}

core::SortDirection sortDirectionFromJson(int value)
{
    return value == static_cast<int>(core::SortDirection::Desc)
        ? core::SortDirection::Desc
        : core::SortDirection::Asc;
}

core::AppSettings settingsFromJson(const QJsonObject &object)
{
    core::AppSettings settings = core::AppSettings::createDefault();
    const QJsonValue lastFolder = object.value(QStringLiteral("lastFolderPath"));
    if (lastFolder.isString()) {
        settings.lastFolderPath = lastFolder.toString();
    } else if (lastFolder.isNull()) {
        settings.lastFolderPath = std::nullopt;
    }

    const QJsonObject sort = object.value(QStringLiteral("sort")).toObject();
    settings.sort = {
        .key = sortKeyFromJson(sort.value(QStringLiteral("key")).toInt()),
        .direction = sortDirectionFromJson(sort.value(QStringLiteral("direction")).toInt()),
    };
    settings.includeSubfolders = object.value(QStringLiteral("includeSubfolders")).toBool(false);
    settings.thumbnailSize = object.value(QStringLiteral("thumbnailSize")).toInt(0);
    return core::settings_rules::normalizeSettings(settings);
}

QJsonObject settingsToJson(const core::AppSettings &settings)
{
    const core::AppSettings normalized = core::settings_rules::normalizeSettings(settings);
    QJsonObject object{
        {QStringLiteral("sort"), QJsonObject{
             {QStringLiteral("key"), static_cast<int>(normalized.sort.key)},
             {QStringLiteral("direction"), static_cast<int>(normalized.sort.direction)},
         }},
        {QStringLiteral("includeSubfolders"), normalized.includeSubfolders},
        {QStringLiteral("thumbnailSize"), normalized.thumbnailSize},
    };
    object.insert(
        QStringLiteral("lastFolderPath"),
        normalized.lastFolderPath.has_value()
            ? QJsonValue(*normalized.lastFolderPath)
            : QJsonValue(QJsonValue::Null));
    return object;
}

} // namespace

JsonSettingsStore::JsonSettingsStore()
    : JsonSettingsStore(app_data_paths::settingsPath())
{
}

JsonSettingsStore::JsonSettingsStore(QString settingsPath)
    : m_settingsPath(std::move(settingsPath))
{
}

const QString &JsonSettingsStore::settingsPath() const
{
    return m_settingsPath;
}

core::AppSettings JsonSettingsStore::load()
{
    return loadWithRecovery().settings;
}

JsonSettingsStore::LoadResult JsonSettingsStore::loadWithRecovery()
{
    QFile file(m_settingsPath);
    if (!file.exists()) {
        return {.settings = core::AppSettings::createDefault()};
    }

    if (!file.open(QIODevice::ReadOnly)) {
        const bool quarantined = quarantineSettingsFile();
        return {
            .settings = core::AppSettings::createDefault(),
            .readFailed = true,
            .quarantined = quarantined,
        };
    }

    QJsonParseError error;
    const QJsonDocument document = QJsonDocument::fromJson(file.readAll(), &error);
    file.close();
    if (error.error != QJsonParseError::NoError || !document.isObject()) {
        const bool quarantined = quarantineSettingsFile();
        return {
            .settings = core::AppSettings::createDefault(),
            .readFailed = true,
            .quarantined = quarantined,
        };
    }

    return {.settings = settingsFromJson(document.object())};
}

void JsonSettingsStore::save(const core::AppSettings &settings)
{
    const QFileInfo info(m_settingsPath);
    if (!QDir().mkpath(info.absolutePath())) {
        throw std::runtime_error("Unable to create the settings directory.");
    }

    QSaveFile file(m_settingsPath);
    if (!file.open(QIODevice::WriteOnly)) {
        throw std::runtime_error("Unable to open the settings file for writing.");
    }
    const QByteArray json = QJsonDocument(settingsToJson(settings)).toJson(QJsonDocument::Indented);
    if (file.write(json) != json.size() || !file.commit()) {
        file.cancelWriting();
        throw std::runtime_error("Unable to atomically save settings.");
    }
}

core::AppSettings JsonSettingsStore::update(const core::AppSettingsPatch &patch)
{
    const LoadResult loaded = loadWithRecovery();
    if (loaded.readFailed && !loaded.quarantined) {
        throw std::runtime_error(
            "Settings update skipped because the existing settings file could not be read or quarantined.");
    }

    const core::AppSettings updated = core::settings_rules::mergeSettingsPatch(loaded.settings, patch);
    save(updated);
    return updated;
}

bool JsonSettingsStore::quarantineSettingsFile()
{
    if (!QFileInfo::exists(m_settingsPath)) {
        return true;
    }
    return QFile::rename(m_settingsPath, uniqueSiblingPath(m_settingsPath, QStringLiteral("corrupt")));
}

} // namespace piclens::infrastructure
