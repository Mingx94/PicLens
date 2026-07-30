#include <piclens/infrastructure/image_animation_probe.h>

#include <QFile>
#include <QFileInfo>
#include <QImageReader>

namespace piclens::infrastructure::image_animation_probe {
namespace {

quint32 littleEndianUInt32(const QByteArray &bytes)
{
    const auto *value = reinterpret_cast<const unsigned char *>(bytes.constData());
    return static_cast<quint32>(value[0])
        | (static_cast<quint32>(value[1]) << 8U)
        | (static_cast<quint32>(value[2]) << 16U)
        | (static_cast<quint32>(value[3]) << 24U);
}

bool isAnimatedWebp(const QString &path)
{
    QFile file(path);
    if (!file.open(QIODevice::ReadOnly)) {
        return false;
    }

    const QByteArray containerHeader = file.read(12);
    if (containerHeader.size() != 12
        || containerHeader.first(4) != QByteArrayLiteral("RIFF")
        || containerHeader.sliced(8, 4) != QByteArrayLiteral("WEBP")) {
        return false;
    }

    const quint64 declaredEnd = static_cast<quint64>(littleEndianUInt32(containerHeader.sliced(4, 4))) + 8U;
    if (declaredEnd > static_cast<quint64>(file.size())) {
        return false;
    }

    int frameCount = 0;
    while (static_cast<quint64>(file.pos()) + 8U <= declaredEnd) {
        const QByteArray chunkHeader = file.read(8);
        if (chunkHeader.size() != 8) {
            return false;
        }
        const quint64 chunkSize = littleEndianUInt32(chunkHeader.sliced(4, 4));
        const quint64 paddedSize = chunkSize + (chunkSize & 1U);
        const quint64 chunkEnd = static_cast<quint64>(file.pos()) + paddedSize;
        if (chunkEnd > declaredEnd) {
            return false;
        }
        if (chunkHeader.first(4) == QByteArrayLiteral("ANMF") && ++frameCount > 1) {
            return true;
        }
        if (!file.seek(static_cast<qint64>(chunkEnd))) {
            return false;
        }
    }
    return false;
}

} // namespace

bool isAnimated(const QString &path)
{
    if (QFileInfo(path).suffix().compare(QStringLiteral("webp"), Qt::CaseInsensitive) == 0) {
        return isAnimatedWebp(path);
    }
    QImageReader reader(path);
    reader.setDecideFormatFromContent(true);
    return reader.canRead() && reader.supportsAnimation() && reader.imageCount() > 1;
}

} // namespace piclens::infrastructure::image_animation_probe
