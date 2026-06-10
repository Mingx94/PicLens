from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "PicLens" / "Assets"


def lerp(a: int, b: int, t: float) -> int:
    return int(a + (b - a) * t)


def rounded_mask(size: tuple[int, int], radius: int) -> Image.Image:
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=radius, fill=255)
    return mask


def vertical_gradient(size: tuple[int, int], top: tuple[int, int, int, int], bottom: tuple[int, int, int, int]) -> Image.Image:
    image = Image.new("RGBA", size)
    pixels = image.load()

    for y in range(size[1]):
        t = y / max(1, size[1] - 1)
        color = tuple(lerp(top[i], bottom[i], t) for i in range(4))
        for x in range(size[0]):
            pixels[x, y] = color

    return image


def paste_masked(base: Image.Image, layer: Image.Image, box: tuple[int, int, int, int], radius: int) -> None:
    width = box[2] - box[0]
    height = box[3] - box[1]
    mask = rounded_mask((width, height), radius)
    base.alpha_composite(Image.composite(layer, Image.new("RGBA", layer.size, (0, 0, 0, 0)), mask), (box[0], box[1]))


def soft_shadow(
    base: Image.Image,
    box: tuple[int, int, int, int],
    radius: int,
    blur: int,
    offset: tuple[int, int],
    color: tuple[int, int, int, int],
) -> None:
    width = box[2] - box[0]
    height = box[3] - box[1]
    mask = rounded_mask((width, height), radius)
    shadow = Image.new("RGBA", base.size, (0, 0, 0, 0))
    shadow_layer = Image.new("RGBA", (width, height), color)
    shadow.alpha_composite(Image.composite(shadow_layer, Image.new("RGBA", shadow_layer.size, (0, 0, 0, 0)), mask), (box[0] + offset[0], box[1] + offset[1]))
    shadow = shadow.filter(ImageFilter.GaussianBlur(blur))
    base.alpha_composite(shadow)


def draw_photo_scene(size: tuple[int, int], radius: int) -> Image.Image:
    scene = vertical_gradient(size, (23, 142, 218, 255), (172, 244, 224, 255))
    draw = ImageDraw.Draw(scene, "RGBA")

    sun_center = (int(size[0] * 0.30), int(size[1] * 0.30))
    sun_radius = int(size[0] * 0.13)
    draw.ellipse(
        (
            sun_center[0] - sun_radius,
            sun_center[1] - sun_radius,
            sun_center[0] + sun_radius,
            sun_center[1] + sun_radius,
        ),
        fill=(255, 201, 74, 255),
        outline=(255, 179, 48, 190),
        width=max(2, size[0] // 180),
    )

    left_peak = [
        (0, size[1]),
        (int(size[0] * 0.26), int(size[1] * 0.58)),
        (int(size[0] * 0.52), size[1]),
    ]
    right_peak = [
        (int(size[0] * 0.18), size[1]),
        (int(size[0] * 0.72), int(size[1] * 0.44)),
        (size[0], int(size[1] * 0.78)),
        (size[0], size[1]),
    ]
    draw.polygon(left_peak, fill=(22, 171, 160, 250))
    draw.polygon(right_peak, fill=(11, 94, 184, 255))
    draw.line(
        [
            (int(size[0] * 0.18), size[1] - 1),
            (int(size[0] * 0.72), int(size[1] * 0.44)),
            (size[0], int(size[1] * 0.78)),
        ],
        fill=(66, 152, 221, 170),
        width=max(2, size[0] // 90),
        joint="curve",
    )

    mask = rounded_mask(size, radius)
    clipped = Image.new("RGBA", size, (0, 0, 0, 0))
    clipped.alpha_composite(Image.composite(scene, clipped, mask))
    return clipped


def make_base_icon(size: int = 1024) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    scale = size / 1024

    def sc(value: int) -> int:
        return int(round(value * scale))

    outer = (sc(108), sc(108), sc(916), sc(916))
    inner = (sc(206), sc(206), sc(818), sc(818))
    frame = (sc(260), sc(266), sc(792), sc(782))
    scene_box = (sc(298), sc(306), sc(754), sc(738))

    soft_shadow(image, outer, sc(190), sc(30), (0, sc(20)), (29, 105, 138, 70))
    outer_layer = vertical_gradient((outer[2] - outer[0], outer[3] - outer[1]), (250, 254, 255, 248), (194, 236, 247, 238))
    paste_masked(image, outer_layer, outer, sc(190))

    draw = ImageDraw.Draw(image, "RGBA")
    draw.rounded_rectangle(outer, radius=sc(190), outline=(170, 223, 241, 180), width=sc(6))
    draw.arc((outer[0] + sc(18), outer[1] + sc(14), outer[2] - sc(18), outer[3] - sc(18)), 190, 355, fill=(255, 255, 255, 120), width=sc(5))

    inner_layer = vertical_gradient((inner[2] - inner[0], inner[3] - inner[1]), (247, 253, 255, 210), (219, 246, 251, 190))
    paste_masked(image, inner_layer, inner, sc(150))
    draw.rounded_rectangle(inner, radius=sc(150), outline=(216, 239, 247, 170), width=sc(3))

    soft_shadow(image, frame, sc(78), sc(26), (0, sc(22)), (0, 71, 110, 65))
    draw.rounded_rectangle(frame, radius=sc(78), fill=(255, 255, 255, 246), outline=(255, 255, 255, 220), width=sc(10))

    scene = draw_photo_scene((scene_box[2] - scene_box[0], scene_box[3] - scene_box[1]), sc(54))
    image.alpha_composite(scene, (scene_box[0], scene_box[1]))
    draw.rounded_rectangle(scene_box, radius=sc(54), outline=(0, 92, 158, 130), width=sc(2))

    return image


def fit_icon_on_canvas(icon: Image.Image, size: tuple[int, int], icon_fraction: float) -> Image.Image:
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    target = int(min(size) * icon_fraction)
    resized = icon.resize((target, target), Image.Resampling.LANCZOS)
    x = (size[0] - target) // 2
    y = (size[1] - target) // 2
    canvas.alpha_composite(resized, (x, y))
    return canvas


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)
    icon = make_base_icon(1024)
    icon.save(
        ASSETS / "AppIcon.ico",
        format="ICO",
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
    )

    outputs = {
        "Square150x150Logo.scale-200.png": ((300, 300), 0.86),
        "Square44x44Logo.scale-200.png": ((88, 88), 0.94),
        "Square44x44Logo.targetsize-24_altform-unplated.png": ((24, 24), 0.96),
        "Square44x44Logo.targetsize-48_altform-lightunplated.png": ((48, 48), 0.96),
        "StoreLogo.png": ((50, 50), 0.92),
        "LockScreenLogo.scale-200.png": ((48, 48), 0.82),
        "Wide310x150Logo.scale-200.png": ((620, 300), 0.70),
        "SplashScreen.scale-200.png": ((1240, 600), 0.42),
    }

    for file_name, (size, fraction) in outputs.items():
        fit_icon_on_canvas(icon, size, fraction).save(ASSETS / file_name)


if __name__ == "__main__":
    main()
