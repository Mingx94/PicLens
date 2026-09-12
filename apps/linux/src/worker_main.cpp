#include <QCoreApplication>
#include <QBuffer>
#include <QDataStream>
#include <QFile>
#include <QFileInfo>
#include <QImageReader>
#include <QImageWriter>
#include <QSaveFile>
#include <QDir>
#include <QTextStream>
#include <webp/encode.h>
#include <algorithm>
#include <cstring>

namespace {
constexpr qint64 MaxRgba = 256LL * 1024 * 1024;
constexpr int TileInterior = 2046; // including both borders fits a 2048 texture
bool fail(const QString &message) { QTextStream(stderr) << message << '\n'; return false; }

QImage decode(const QString &path, int edge) {
    // Preview identity stays 1024. Reserve both border pixels so three
    // square preview tile allocations fit within 12 MiB, including borders.
    const int pixelEdge = edge == 1024 ? 1022 : edge;
    QImageReader reader(path);
    reader.setAutoTransform(true);
    if (!reader.canRead()) { fail(reader.errorString()); return {}; }
    if (reader.supportsAnimation() && reader.imageCount() != 1) {
        fail(QStringLiteral("不支援動畫圖片")); return {};
    }
    const QSize size = reader.size();
    if (size.width() <= 0 || size.height() <= 0 ||
        (edge == 0 && qint64(size.width()) * size.height() > MaxRgba / 4)) {
        fail(QStringLiteral("原圖超過 256 MiB 或尺寸無效")); return {};
    }
    if (pixelEdge > 0 && (size.width() > pixelEdge || size.height() > pixelEdge))
        reader.setScaledSize(size.scaled(pixelEdge, pixelEdge, Qt::KeepAspectRatio));
    QImage image = reader.read();
    if (image.isNull()) { fail(reader.errorString()); return {}; }
    if (pixelEdge > 0 && (image.width() > pixelEdge || image.height() > pixelEdge))
        image = image.scaled(pixelEdge, pixelEdge, Qt::KeepAspectRatio, Qt::SmoothTransformation);
    if (qint64(image.width()) * image.height() > MaxRgba / 4) {
        fail(QStringLiteral("原圖超過 256 MiB")); return {};
    }
    return image;
}

bool transport(const QImage &input, const QString &output) {
    // Exclusive create: never overwrite a caller-supplied path, including source.
    QFile file(output);
    if (!file.open(QIODevice::WriteOnly | QIODevice::NewOnly)) return fail(file.errorString());
    QDataStream stream(&file);
    stream.setByteOrder(QDataStream::LittleEndian);
    const auto image = input.convertToFormat(QImage::Format_RGBA8888_Premultiplied);
    const int nx = (image.width() + TileInterior - 1) / TileInterior;
    const int ny = (image.height() + TileInterior - 1) / TileInterior;
    stream << quint32(0x504C5431) << qint32(image.width()) << qint32(image.height()) << qint32(nx * ny);
    for (int y = 0; y < image.height(); y += TileInterior) {
        for (int x = 0; x < image.width(); x += TileInterior) {
            const int w = std::min(TileInterior, image.width() - x);
            const int h = std::min(TileInterior, image.height() - y);
            QImage tile(w + 2, h + 2, QImage::Format_RGBA8888_Premultiplied);
            if (tile.isNull()) return fail(QStringLiteral("分塊記憶體不足"));
            for (int row = 0; row < h + 2; ++row) {
                const auto *src = image.constScanLine(std::clamp(y + row - 1, 0, image.height() - 1));
                auto *dst = tile.scanLine(row);
                std::memcpy(dst, src + std::max(x - 1, 0) * 4, 4);
                std::memcpy(dst + 4, src + x * 4, size_t(w) * 4);
                std::memcpy(dst + (w + 1) * 4, src + std::min(x + w, image.width() - 1) * 4, 4);
            }
            stream << qint32(x) << qint32(y) << qint32(w) << qint32(h) << qint32(tile.bytesPerLine());
            if (stream.writeRawData(reinterpret_cast<const char *>(tile.constBits()), tile.sizeInBytes()) != tile.sizeInBytes())
                return fail(QStringLiteral("無法寫入像素"));
        }
    }
    return stream.status() == QDataStream::Ok && file.flush();
}

bool convert(const QString &source, const QString &target, const QString &format, bool existingTemporary = false) {
    if (format != "jpg" && format != "webp") return fail(QStringLiteral("不支援的轉換格式"));
    const QString suffix = QFileInfo(source).suffix().toLower();
    if (format == "webp" && (suffix == "jpg" || suffix == "jpeg" || suffix == "webp"))
        return fail(QStringLiteral("略過 JPG、JPEG 與既有 WebP"));
    QImage image = decode(source, 0);
    if (image.isNull()) return false;
    QByteArray encoded;
    if (format == "webp") {
        const QImage rgba = image.convertToFormat(QImage::Format_RGBA8888);
        WebPConfig config;
        WebPPicture picture;
        if (!WebPConfigInit(&config) || !WebPPictureInit(&picture)) return fail("WebP 初始化失敗");
        config.lossless = 1;
        config.quality = 100;
        config.exact = 1; // retain RGB even under fully transparent pixels
        picture.use_argb = 1;
        picture.width = rgba.width(); picture.height = rgba.height();
        WebPMemoryWriter writer;
        WebPMemoryWriterInit(&writer);
        picture.writer = WebPMemoryWrite; picture.custom_ptr = &writer;
        const bool ok = WebPPictureImportRGBA(&picture, rgba.constBits(), rgba.bytesPerLine()) && WebPEncode(&config, &picture);
        if (ok) encoded = QByteArray(reinterpret_cast<const char *>(writer.mem), qsizetype(writer.size));
        WebPPictureFree(&picture); WebPMemoryWriterClear(&writer);
        if (!ok) return fail(QStringLiteral("WebP 編碼失敗"));
    } else {
        // Encode before acquiring the destination. A codec failure cannot leave a target.
        QBuffer buffer(&encoded);
        buffer.open(QIODevice::WriteOnly);
        QImageWriter writer(&buffer, "jpg"); writer.setQuality(100);
        if (!writer.write(image)) return fail(writer.errorString());
    }
    QFile file(target);
    if (existingTemporary) {
        const QFileInfo info(target);
        if (info.isSymLink() || !info.isFile() || info.size() != 0 || info.canonicalFilePath() == QFileInfo(source).canonicalFilePath())
            return fail(QStringLiteral("暫存輸出必須是已存在的專用空檔"));
        // ReadWrite avoids WriteOnly's implicit truncation; check the opened
        // file again so a replaced non-empty source can never be truncated.
        if (!file.open(QIODevice::ReadWrite | QIODevice::ExistingOnly) || file.size() != 0)
            return fail(QStringLiteral("暫存輸出已變更或無法開啟"));
    } else if (!file.open(QIODevice::WriteOnly | QIODevice::NewOnly)) return fail(QStringLiteral("目標已存在或無法建立：") + file.errorString());
    // On interrupted/failed writes retain the new target; never remove a path
    // that another process could have replaced. The caller reports partial output.
    if (file.write(encoded) != encoded.size() || !file.flush()) return fail(QStringLiteral("目標寫入不完整"));
    return true;
}
}

int main(int argc, char **argv) {
    QCoreApplication app(argc, argv);
    QImageReader::setAllocationLimit(512);
    const auto args = app.arguments();
    if (args.size() == 5 && args[1] == "--convert") return convert(args[2], args[3], args[4]) ? 0 : 1;
    if (args.size() == 5 && args[1] == "--encode-temp") return convert(args[2], args[3], args[4], true) ? 0 : 1;
    if (args.size() != 6 || args[1] != "--decode") { fail("Usage: --decode source edge output cache | --convert source target jpg|webp"); return 2; }
    bool valid = false;
    const int edge = args[3].toInt(&valid);
    if (!valid || edge < 0 || edge > 1024) { fail("Invalid edge"); return 2; }
    const QString source = args[2], output = args[4], cache = args[5];
    QImage image;
    // Warm PNG is decoded in this killable process, never on the GUI thread.
    if (edge > 0 && !cache.isEmpty() && QFileInfo::exists(cache)) image = decode(cache, edge);
    if (image.isNull()) {
        image = decode(source, edge);
        if (image.isNull()) return 1;
        if (edge > 0 && !cache.isEmpty() && QFileInfo(cache).absoluteFilePath() != QFileInfo(source).absoluteFilePath()) {
            // PNG is cache-only. Exclusive creation also protects symlinks and source aliases.
            QFile cached(cache);
            if (cached.open(QIODevice::WriteOnly | QIODevice::NewOnly)) {
                if (image.save(&cached, "PNG") && cached.flush()) QTextStream(stdout) << "cache-written\n";
            }
        }
    }
    return transport(image, output) ? 0 : 1;
}
