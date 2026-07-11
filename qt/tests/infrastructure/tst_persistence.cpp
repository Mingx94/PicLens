#include <piclens/core/settings_rules.h>
#include <piclens/infrastructure/app_data_paths.h>
#include <piclens/infrastructure/file_app_logger.h>
#include <piclens/infrastructure/json_settings_store.h>

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QStandardPaths>
#include <QTemporaryDir>
#include <QTest>

#include <stdexcept>

using namespace piclens::core;
using namespace piclens::infrastructure;

namespace {

class EnvironmentScope final
{
public:
    EnvironmentScope(const char *name, const QByteArray &value)
        : m_name(name)
        , m_hadValue(qEnvironmentVariableIsSet(name))
        , m_previous(qgetenv(name))
    {
        qputenv(m_name.constData(), value);
    }

    ~EnvironmentScope()
    {
        if (m_hadValue) {
            qputenv(m_name.constData(), m_previous);
        } else {
            qunsetenv(m_name.constData());
        }
    }

    EnvironmentScope(const EnvironmentScope &) = delete;
    EnvironmentScope &operator=(const EnvironmentScope &) = delete;

private:
    QByteArray m_name;
    bool m_hadValue;
    QByteArray m_previous;
};

QString childPath(const QString &directory, const QString &name)
{
    return QDir(directory).filePath(name);
}

void compareSettings(const AppSettings &expected, const AppSettings &actual)
{
    QCOMPARE(actual.lastFolderPath, expected.lastFolderPath);
    QCOMPARE(actual.sort, expected.sort);
    QCOMPARE(actual.includeSubfolders, expected.includeSubfolders);
    QCOMPARE(actual.thumbnailSize, expected.thumbnailSize);
}

} // namespace

class PersistenceTests final : public QObject
{
    Q_OBJECT

private slots:
    void appDataPathsUseConfiguredRoot();
    void appDataPathsUsePlatformLocalDataByDefault();
    void defaultStoreUsesPicLensSettingsPath();
    void missingSettingsReturnDefaults();
    void invalidSettingsAreQuarantined();
    void updateMergesAndPersistsSettings();
    void loadsAvaloniaSettingsSchemaWithoutMigration();
    void legacyFavoriteFoldersAreIgnored();
    void loggerDefaultPathUsesAppDataRoot();
    void loggerWritesTimestampLevelContextAndDetails();
    void loggerDisposesAfterManyMessages();
};

void PersistenceTests::appDataPathsUseConfiguredRoot()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const EnvironmentScope environment(
        app_data_paths::DataRootEnvironmentVariable,
        root.path().toUtf8());

    const QString expectedRoot = QFileInfo(root.path()).absoluteFilePath();
    QCOMPARE(app_data_paths::appRoot(), expectedRoot);
    QCOMPARE(app_data_paths::settingsPath(), childPath(expectedRoot, QStringLiteral("piclens-settings.json")));
    QCOMPARE(app_data_paths::logPath(), childPath(expectedRoot, QStringLiteral("Logs/PicLens.log")));
    QCOMPARE(app_data_paths::thumbnailCacheRoot(), childPath(expectedRoot, QStringLiteral("Thumbnails")));
}

void PersistenceTests::appDataPathsUsePlatformLocalDataByDefault()
{
    const bool hadValue = qEnvironmentVariableIsSet(app_data_paths::DataRootEnvironmentVariable);
    const QByteArray previous = qgetenv(app_data_paths::DataRootEnvironmentVariable);
    qunsetenv(app_data_paths::DataRootEnvironmentVariable);

    QString localData = QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation);
    if (localData.trimmed().isEmpty()) {
        localData = QDir::tempPath();
    }
    QCOMPARE(app_data_paths::appRoot(), childPath(localData, QStringLiteral("PicLens")));

    if (hadValue) {
        qputenv(app_data_paths::DataRootEnvironmentVariable, previous);
    }
}

void PersistenceTests::defaultStoreUsesPicLensSettingsPath()
{
    const JsonSettingsStore store;
    QVERIFY(QDir::fromNativeSeparators(store.settingsPath())
                .endsWith(QStringLiteral("PicLens/piclens-settings.json"), Qt::CaseInsensitive));
}

void PersistenceTests::missingSettingsReturnDefaults()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    JsonSettingsStore store(childPath(root.path(), QStringLiteral("settings.json")));
    compareSettings(AppSettings::createDefault(), store.load());
}

void PersistenceTests::invalidSettingsAreQuarantined()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString settingsPath = childPath(root.path(), QStringLiteral("settings.json"));
    QFile file(settingsPath);
    QVERIFY(file.open(QIODevice::WriteOnly));
    QCOMPARE(file.write("{ invalid json"), 14);
    file.close();

    JsonSettingsStore store(settingsPath);
    compareSettings(AppSettings::createDefault(), store.load());
    QVERIFY(!QFileInfo::exists(settingsPath));

    const QStringList quarantined = QDir(root.path()).entryList(
        {QStringLiteral("settings.json.corrupt.*")},
        QDir::Files);
    QCOMPARE(quarantined.size(), 1);
    QFile quarantinedFile(childPath(root.path(), quarantined.constFirst()));
    QVERIFY(quarantinedFile.open(QIODevice::ReadOnly));
    QCOMPARE(quarantinedFile.readAll(), QByteArray("{ invalid json"));

    compareSettings(AppSettings::createDefault(), store.load());
    QCOMPARE(
        QDir(root.path()).entryList({QStringLiteral("settings.json.corrupt.*")}, QDir::Files).size(),
        1);
}

void PersistenceTests::updateMergesAndPersistsSettings()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    JsonSettingsStore store(childPath(root.path(), QStringLiteral("settings.json")));
    AppSettingsPatch patch;
    patch.lastFolderPath = root.path();
    patch.hasLastFolderPath = true;
    patch.includeSubfolders = true;
    patch.sort = SortState{.key = SortKey::ModifiedAt, .direction = SortDirection::Desc};
    patch.thumbnailSize = 188;

    const AppSettings updated = store.update(patch);
    const AppSettings loaded = store.load();

    QCOMPARE(updated.lastFolderPath.value_or(QString()), root.path());
    QVERIFY(updated.includeSubfolders);
    QCOMPARE(updated.thumbnailSize, 180);
    compareSettings(updated, loaded);
    QCOMPARE(QDir(root.path()).entryList({QStringLiteral("*.tmp")}, QDir::Files).size(), 0);
}

void PersistenceTests::loadsAvaloniaSettingsSchemaWithoutMigration()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString settingsPath = childPath(root.path(), QStringLiteral("settings.json"));
    QFile file(settingsPath);
    QVERIFY(file.open(QIODevice::WriteOnly));
    file.write(R"({
      "lastFolderPath": "C:\\Images",
      "sort": { "key": 1, "direction": 1 },
      "includeSubfolders": true,
      "thumbnailSize": 240
    })");
    file.close();

    const AppSettings loaded = JsonSettingsStore(settingsPath).load();
    QCOMPARE(loaded.lastFolderPath.value_or(QString()), QStringLiteral("C:\\Images"));
    QCOMPARE(loaded.sort, SortState({.key = SortKey::ModifiedAt, .direction = SortDirection::Desc}));
    QVERIFY(loaded.includeSubfolders);
    QCOMPARE(loaded.thumbnailSize, 240);
}

void PersistenceTests::legacyFavoriteFoldersAreIgnored()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString settingsPath = childPath(root.path(), QStringLiteral("settings.json"));
    QFile file(settingsPath);
    QVERIFY(file.open(QIODevice::WriteOnly));
    file.write(R"({
      "version": 1,
      "lastFolderPath": "C:\\Images",
      "sort": { "key": 0, "direction": 0 },
      "includeSubfolders": false,
      "favoriteFolders": [{ "id": "user:old", "path": "C:\\Old" }]
    })");
    file.close();

    const AppSettings loaded = JsonSettingsStore(settingsPath).load();
    QCOMPARE(loaded.lastFolderPath.value_or(QString()), QStringLiteral("C:\\Images"));
    QCOMPARE(loaded.sort, SortState({.key = SortKey::Name, .direction = SortDirection::Asc}));
    QVERIFY(!loaded.includeSubfolders);
    QCOMPARE(loaded.thumbnailSize, settings_rules::DefaultThumbnailSize);
}

void PersistenceTests::loggerDefaultPathUsesAppDataRoot()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const EnvironmentScope environment(
        app_data_paths::DataRootEnvironmentVariable,
        root.path().toUtf8());
    QCOMPARE(
        FileAppLogger::defaultLogPath(),
        childPath(root.path(), QStringLiteral("Logs/PicLens.log")));
}

void PersistenceTests::loggerWritesTimestampLevelContextAndDetails()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString logPath = childPath(root.path(), QStringLiteral("PicLens.log"));
    {
        FileAppLogger logger(logPath, [] {
            return QDateTime::fromString(
                QStringLiteral("2026-06-06T12:34:56.000+08:00"),
                Qt::ISODateWithMs);
        });
        const std::runtime_error exception("boom");
        logger.error(exception, QStringLiteral("IncludeSubfoldersChanged"));
    }

    QFile file(logPath);
    QVERIFY(file.open(QIODevice::ReadOnly));
    const QString log = QString::fromUtf8(file.readAll());
    QVERIFY(log.contains(QStringLiteral("2026-06-06T12:34:56.000+08:00")));
    QVERIFY(log.contains(QStringLiteral("[ERROR]")));
    QVERIFY(log.contains(QStringLiteral("IncludeSubfoldersChanged")));
    QVERIFY(log.contains(QStringLiteral("boom")));
}

void PersistenceTests::loggerDisposesAfterManyMessages()
{
    QTemporaryDir root;
    QVERIFY(root.isValid());
    const QString logPath = childPath(root.path(), QStringLiteral("PicLens.log"));
    {
        FileAppLogger logger(logPath);
        for (int index = 0; index < 6000; ++index) {
            logger.info(QStringLiteral("message-%1").arg(index));
        }
    }

    QFile file(logPath);
    QVERIFY(file.open(QIODevice::ReadOnly));
    QVERIFY(!file.readAll().isEmpty());
}

QTEST_GUILESS_MAIN(PersistenceTests)

#include "tst_persistence.moc"
