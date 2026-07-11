#include <QtQuickTest/quicktest.h>
#include <QQuickStyle>

class QuickTestSetup final : public QObject
{
    Q_OBJECT

public slots:
    void applicationAvailable()
    {
        QQuickStyle::setStyle(QStringLiteral("Basic"));
    }
};

QUICK_TEST_MAIN_WITH_SETUP(piclens_qml_tests, QuickTestSetup)

#include "tst_qml_main.moc"
