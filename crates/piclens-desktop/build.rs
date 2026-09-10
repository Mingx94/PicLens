fn main() {
    println!("cargo:rerun-if-changed=../../assets/AppIcon.ico");
    if std::env::var("CARGO_CFG_TARGET_OS").as_deref() == Ok("windows") {
        winresource::WindowsResource::new()
            .set_icon("../../assets/AppIcon.ico")
            .compile()
            .expect("failed to embed the PicLens Windows icon");
    }
}
