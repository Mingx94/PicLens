use std::fs::File;
use std::io::{Read, Seek, SeekFrom};
use std::path::Path;

fn little_endian_u32(bytes: &[u8]) -> u32 {
    u32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]])
}

fn is_animated_webp(path: &Path) -> bool {
    let mut file = match File::open(path) {
        Ok(f) => f,
        Err(_) => return false,
    };
    let mut header = [0u8; 12];
    if file.read_exact(&mut header).is_err() {
        return false;
    }
    if &header[0..4] != b"RIFF" || &header[8..12] != b"WEBP" {
        return false;
    }
    let declared_end = u64::from(little_endian_u32(&header[4..8])) + 8;
    let file_len = file.metadata().map(|m| m.len()).unwrap_or(0);
    if declared_end > file_len {
        return false;
    }

    let mut frame_count = 0u32;
    loop {
        let pos = file.stream_position().unwrap_or(0);
        if pos + 8 > declared_end {
            break;
        }
        let mut chunk_header = [0u8; 8];
        if file.read_exact(&mut chunk_header).is_err() {
            return false;
        }
        let chunk_size = u64::from(little_endian_u32(&chunk_header[4..8]));
        let padded = chunk_size + (chunk_size & 1);
        let chunk_end = pos + 8 + padded;
        if chunk_end > declared_end {
            return false;
        }
        if &chunk_header[0..4] == b"ANMF" {
            frame_count += 1;
            if frame_count > 1 {
                return true;
            }
        }
        if file.seek(SeekFrom::Start(chunk_end)).is_err() {
            return false;
        }
    }
    false
}

fn is_animated_gif(path: &Path) -> bool {
    // Count image descriptors roughly; >1 means animation.
    let Ok(bytes) = std::fs::read(path) else {
        return false;
    };
    if bytes.len() < 6 || &bytes[0..3] != b"GIF" {
        return false;
    }
    let mut i = 13usize; // skip header + logical screen
    if bytes.len() > 10 && bytes[10] & 0x80 != 0 {
        let gct_size = 3 * (1 << ((bytes[10] & 0x07) + 1));
        i += gct_size;
    }
    let mut frames = 0u32;
    while i < bytes.len() {
        match bytes[i] {
            0x3B => break, // trailer
            0x21 => {
                // extension
                i += 2;
                while i < bytes.len() {
                    let block = bytes[i] as usize;
                    i += 1;
                    if block == 0 {
                        break;
                    }
                    i += block;
                }
            }
            0x2C => {
                frames += 1;
                if frames > 1 {
                    return true;
                }
                if i + 10 >= bytes.len() {
                    break;
                }
                let packed = bytes[i + 9];
                i += 10;
                if packed & 0x80 != 0 {
                    let lct = 3 * (1 << ((packed & 0x07) + 1));
                    i += lct;
                }
                i += 1; // LZW min code size
                while i < bytes.len() {
                    let block = bytes[i] as usize;
                    i += 1;
                    if block == 0 {
                        break;
                    }
                    i += block;
                }
            }
            _ => break,
        }
    }
    false
}

pub fn is_animated(path: &Path) -> bool {
    let ext = path
        .extension()
        .and_then(|e| e.to_str())
        .unwrap_or("")
        .to_ascii_lowercase();
    match ext.as_str() {
        "webp" => is_animated_webp(path),
        "gif" => is_animated_gif(path),
        _ => false,
    }
}
