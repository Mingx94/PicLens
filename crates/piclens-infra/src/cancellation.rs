use std::sync::{
    atomic::{AtomicBool, Ordering},
    Arc,
};

/// A small cooperative cancellation token for blocking filesystem work.
///
/// Blocking library calls cannot be interrupted safely. Callers must check the
/// token before every new side effect and after expensive read/decode work.
#[derive(Clone, Debug, Default)]
pub struct CancellationToken {
    canceled: Arc<AtomicBool>,
}

impl CancellationToken {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn cancel(&self) {
        self.canceled.store(true, Ordering::Release);
    }

    pub fn is_canceled(&self) -> bool {
        self.canceled.load(Ordering::Acquire)
    }
}

#[cfg(test)]
mod tests {
    use super::CancellationToken;

    #[test]
    fn clones_share_cancellation_state() {
        let token = CancellationToken::new();
        let clone = token.clone();
        assert!(!clone.is_canceled());
        token.cancel();
        assert!(clone.is_canceled());
    }
}
