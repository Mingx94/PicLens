use std::cmp::Ordering;

use crate::models::{ListItem, SortDirection, SortKey, SortState};

fn first_significant_digit(chars: &[char], start: usize, end: usize) -> usize {
    let mut index = start;
    while index + 1 < end && chars[index] == '0' {
        index += 1;
    }
    index
}

fn compare_number_runs(left: &[char], li: &mut usize, right: &[char], ri: &mut usize) -> Ordering {
    let left_start = *li;
    while *li < left.len() && left[*li].is_ascii_digit() {
        *li += 1;
    }
    let right_start = *ri;
    while *ri < right.len() && right[*ri].is_ascii_digit() {
        *ri += 1;
    }

    let left_sig = first_significant_digit(left, left_start, *li);
    let right_sig = first_significant_digit(right, right_start, *ri);
    let left_sig_len = *li - left_sig;
    let right_sig_len = *ri - right_sig;
    match left_sig_len.cmp(&right_sig_len) {
        Ordering::Equal => {}
        other => return other,
    }
    for offset in 0..left_sig_len {
        match (left[left_sig + offset] as u32).cmp(&(right[right_sig + offset] as u32)) {
            Ordering::Equal => {}
            other => return other,
        }
    }
    let left_run = *li - left_start;
    let right_run = *ri - right_start;
    match right_run.cmp(&left_run) {
        Ordering::Equal => left[left_start..*li].cmp(&right[right_start..*ri]),
        other => other,
    }
}

/// Windows Explorer–style natural compare (digit runs, case-insensitive letters).
pub fn natural_compare(left: &str, right: &str) -> Ordering {
    let left: Vec<char> = left.chars().collect();
    let right: Vec<char> = right.chars().collect();
    let mut li = 0usize;
    let mut ri = 0usize;

    while li < left.len() && ri < right.len() {
        if left[li].is_ascii_digit() && right[ri].is_ascii_digit() {
            let result = compare_number_runs(&left, &mut li, &right, &mut ri);
            if result != Ordering::Equal {
                return result;
            }
            continue;
        }

        let left_upper = left[li].to_uppercase().next().unwrap_or(left[li]);
        let right_upper = right[ri].to_uppercase().next().unwrap_or(right[ri]);
        match (left_upper as u32).cmp(&(right_upper as u32)) {
            Ordering::Equal => {}
            other => return other,
        }
        match (left[li] as u32).cmp(&(right[ri] as u32)) {
            Ordering::Equal => {}
            other => return other,
        }
        li += 1;
        ri += 1;
    }

    (left.len() - li).cmp(&(right.len() - ri))
}

pub fn sort_items(
    items: &[ListItem],
    sort_state: SortState,
    keep_folders_first: bool,
) -> Vec<ListItem> {
    let mut sorted = items.to_vec();
    sorted.sort_by(|left, right| {
        if keep_folders_first && left.is_folder() != right.is_folder() {
            return if left.is_folder() {
                Ordering::Less
            } else {
                Ordering::Greater
            };
        }

        let order = match sort_state.key {
            SortKey::Name => natural_compare(left.name(), right.name()),
            SortKey::ModifiedAt => left
                .modified_at_ms()
                .unwrap_or(0)
                .cmp(&right.modified_at_ms().unwrap_or(0)),
        };

        match sort_state.direction {
            SortDirection::Asc => order,
            SortDirection::Desc => order.reverse(),
        }
    });
    sorted
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::{FolderListItem, ImageListItem, SortDirection, SortKey};

    #[test]
    fn natural_order_puts_2_before_10() {
        assert_eq!(natural_compare("img2.jpg", "img10.jpg"), Ordering::Less);
    }

    #[test]
    fn folders_first_when_requested() {
        let items = vec![
            ListItem::Image(ImageListItem {
                path: "a.jpg".into(),
                name: "a.jpg".into(),
                extension: "jpg".into(),
                modified_at_ms: None,
                size_bytes: 1,
                is_animated: false,
            }),
            ListItem::Folder(FolderListItem {
                path: "z".into(),
                name: "z".into(),
                modified_at_ms: None,
            }),
        ];
        let sorted = sort_items(&items, SortState::default(), true);
        assert!(sorted[0].is_folder());
    }

    #[test]
    fn modified_sort_descending() {
        let items = vec![
            ListItem::Image(ImageListItem {
                path: "old.jpg".into(),
                name: "old.jpg".into(),
                extension: "jpg".into(),
                modified_at_ms: Some(10),
                size_bytes: 1,
                is_animated: false,
            }),
            ListItem::Image(ImageListItem {
                path: "new.jpg".into(),
                name: "new.jpg".into(),
                extension: "jpg".into(),
                modified_at_ms: Some(99),
                size_bytes: 1,
                is_animated: false,
            }),
        ];
        let sorted = sort_items(
            &items,
            SortState {
                key: SortKey::ModifiedAt,
                direction: SortDirection::Desc,
            },
            false,
        );
        assert_eq!(sorted[0].name(), "new.jpg");
    }
}
