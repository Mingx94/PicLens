fn main() {
    let target_os = std::env::var("CARGO_CFG_TARGET_OS").unwrap_or_default();
    if target_os != "windows" {
        return;
    }
    let icon = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../../assets/AppIcon.ico");
    println!("cargo:rerun-if-changed={}", icon.display());
    let mut resource = winresource::WindowsResource::new();
    resource.set_icon(icon.to_str().expect("AppIcon.ico path is valid UTF-8"));
    resource
        .compile()
        .expect("embed AppIcon.ico as Windows resource 1");
}
