//! Explicit CLI screenshot capture for native runtime validation.

use std::path::Path;

use image::{ImageBuffer, Rgb, Rgba};
use scap::capturer::{Area, Capturer, Options, Point, Resolution, Size};
use scap::frame::{Frame, FrameType};

pub fn capture(path: &Path, area: (f64, f64, f64, f64)) -> Result<(), String> {
    if !scap::is_supported() {
        return Err("screen capture is not supported on this desktop session".into());
    }
    if !scap::has_permission() && !scap::request_permission() {
        return Err("screen capture permission was denied".into());
    }
    let mut capturer = Capturer::build(Options {
        fps: 1,
        show_cursor: false,
        show_highlight: false,
        crop_area: Some(Area {
            origin: Point {
                x: area.0,
                y: area.1,
            },
            size: Size {
                width: area.2,
                height: area.3,
            },
        }),
        output_type: FrameType::BGRAFrame,
        output_resolution: Resolution::Captured,
        ..Default::default()
    })
    .map_err(|err| err.to_string())?;
    capturer.start_capture();
    let frame = capturer.get_next_frame().map_err(|err| err.to_string());
    capturer.stop_capture();
    let frame = frame?;
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent).map_err(|err| err.to_string())?;
    }
    match frame {
        Frame::BGRA(frame) => {
            let mut data = frame.data;
            for pixel in data.as_chunks_mut::<4>().0 {
                pixel.swap(0, 2);
            }
            ImageBuffer::<Rgba<u8>, _>::from_raw(frame.width as u32, frame.height as u32, data)
                .ok_or_else(|| "invalid BGRA screenshot buffer".to_string())?
                .save(path)
                .map_err(|err| err.to_string())
        }
        Frame::BGR0(frame) => {
            let mut rgb = Vec::with_capacity((frame.width * frame.height * 3) as usize);
            for pixel in frame.data.as_chunks::<4>().0 {
                rgb.extend_from_slice(&[pixel[2], pixel[1], pixel[0]]);
            }
            ImageBuffer::<Rgb<u8>, _>::from_raw(frame.width as u32, frame.height as u32, rgb)
                .ok_or_else(|| "invalid BGR0 screenshot buffer".to_string())?
                .save(path)
                .map_err(|err| err.to_string())
        }
        other => Err(format!("unsupported screenshot frame: {other:?}")),
    }
}
