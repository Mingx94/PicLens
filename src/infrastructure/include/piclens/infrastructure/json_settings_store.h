#pragma once

#include <piclens/core/models.h>

#include <QString>

namespace piclens::infrastructure {

class JsonSettingsStore final
{
public:
    JsonSettingsStore();
    explicit JsonSettingsStore(QString settingsPath);

    [[nodiscard]] const QString &settingsPath() const;
    [[nodiscard]] core::AppSettings load();
    [[nodiscard]] core::AppSettings update(const core::AppSettingsPatch &patch);

private:
    struct LoadResult {
        core::AppSettings settings;
        bool readFailed = false;
        bool quarantined = false;
    };

    [[nodiscard]] LoadResult loadWithRecovery();
    void save(const core::AppSettings &settings);
    [[nodiscard]] bool quarantineSettingsFile();

    QString m_settingsPath;
};

} // namespace piclens::infrastructure
