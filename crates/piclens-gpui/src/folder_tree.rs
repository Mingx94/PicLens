//! Expandable folder-tree rows under the picker folder.

use std::collections::{HashMap, HashSet};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TreeRow {
    pub path: String,
    pub depth: usize,
    pub expanded: bool,
    pub expandable: bool,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ExpandAction {
    Collapse,
    NeedChildren,
}

pub fn toggle_expand(expanded: &mut HashSet<String>, path: &str) -> ExpandAction {
    if expanded.remove(path) {
        ExpandAction::Collapse
    } else {
        expanded.insert(path.to_string());
        ExpandAction::NeedChildren
    }
}

pub fn apply_tree_children(
    children_by_parent: &mut HashMap<String, Vec<String>>,
    parent: &str,
    children: Vec<String>,
) {
    children_by_parent.insert(parent.to_string(), children);
}

/// Rebuild the tree only when the folder picker (or startup restore of that
/// picker folder) supplies a new root. Navigation must not call this with
/// `remember_picker == false`.
pub fn replace_tree_for_picker(
    remember_picker: bool,
    tree_root: &mut Option<String>,
    roots: &mut Vec<String>,
    children_by_parent: &mut HashMap<String, Vec<String>>,
    expanded: &mut HashSet<String>,
    picker_path: &str,
    new_roots: Vec<String>,
) -> bool {
    if !remember_picker {
        return false;
    }
    *tree_root = Some(picker_path.to_string());
    *roots = vec![picker_path.to_string()];
    children_by_parent.clear();
    children_by_parent.insert(picker_path.to_string(), new_roots);
    expanded.clear();
    expanded.insert(picker_path.to_string());
    true
}

pub fn visible_tree_rows(
    roots: &[String],
    children_by_parent: &HashMap<String, Vec<String>>,
    expanded: &HashSet<String>,
) -> Vec<TreeRow> {
    let mut rows = Vec::new();
    for path in roots {
        push_rows(path, 0, children_by_parent, expanded, &mut rows);
    }
    rows
}

fn push_rows(
    path: &str,
    depth: usize,
    children_by_parent: &HashMap<String, Vec<String>>,
    expanded: &HashSet<String>,
    rows: &mut Vec<TreeRow>,
) {
    let children = children_by_parent.get(path);
    let is_root = depth == 0;
    let is_expanded = is_root || expanded.contains(path);
    rows.push(TreeRow {
        path: path.to_string(),
        depth,
        expanded: is_expanded,
        expandable: !is_root,
    });
    if is_expanded {
        if let Some(children) = children {
            for child in children {
                push_rows(child, depth + 1, children_by_parent, expanded, rows);
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use std::collections::{HashMap, HashSet};

    use super::*;

    #[test]
    fn expanding_a_folder_exposes_its_child_paths() {
        let roots = vec!["/root".into()];
        let mut children = HashMap::new();
        apply_tree_children(
            &mut children,
            "/root",
            vec!["/root/a".into(), "/root/b".into()],
        );
        apply_tree_children(
            &mut children,
            "/root/a",
            vec!["/root/a/one".into(), "/root/a/two".into()],
        );
        let mut expanded = HashSet::new();
        assert_eq!(
            toggle_expand(&mut expanded, "/root/a"),
            ExpandAction::NeedChildren
        );
        let rows = visible_tree_rows(&roots, &children, &expanded);
        let paths: Vec<&str> = rows.iter().map(|row| row.path.as_str()).collect();
        assert_eq!(
            paths,
            vec!["/root", "/root/a", "/root/a/one", "/root/a/two", "/root/b"]
        );
        assert_eq!(rows[2].depth, 2);
        assert_eq!(
            toggle_expand(&mut expanded, "/root/a"),
            ExpandAction::Collapse
        );
        let collapsed = visible_tree_rows(&roots, &children, &expanded);
        assert_eq!(collapsed.len(), 3);
    }

    #[test]
    fn root_is_always_expanded_and_not_expandable() {
        let roots = vec!["/root".into()];
        let mut children = HashMap::new();
        apply_tree_children(&mut children, "/root", vec!["/root/child".into()]);

        let rows = visible_tree_rows(&roots, &children, &HashSet::new());

        assert_eq!(rows.len(), 2);
        assert!(rows[0].expanded);
        assert!(!rows[0].expandable);
        assert!(rows[1].expandable);
    }

    #[test]
    fn picker_replaces_tree_navigation_does_not() {
        let mut tree_root = Some("/old".into());
        let mut roots = vec!["/old/a".into()];
        let mut children = HashMap::new();
        apply_tree_children(&mut children, "/old/a", vec!["/old/a/one".into()]);
        let mut expanded = HashSet::from(["/old/a".into()]);

        assert!(!replace_tree_for_picker(
            false,
            &mut tree_root,
            &mut roots,
            &mut children,
            &mut expanded,
            "/old/a/one",
            vec!["/should-not-apply".into()],
        ));
        assert_eq!(tree_root.as_deref(), Some("/old"));
        assert_eq!(roots, vec!["/old/a".to_string()]);
        assert!(expanded.contains("/old/a"));
        assert_eq!(children.get("/old/a").map(Vec::len), Some(1));

        assert!(replace_tree_for_picker(
            true,
            &mut tree_root,
            &mut roots,
            &mut children,
            &mut expanded,
            "/picked",
            vec!["/picked/x".into(), "/picked/y".into()],
        ));
        assert_eq!(tree_root.as_deref(), Some("/picked"));
        assert_eq!(roots, vec!["/picked".to_string()]);
        assert_eq!(
            children.get("/picked"),
            Some(&vec!["/picked/x".to_string(), "/picked/y".to_string()])
        );
        assert!(expanded.contains("/picked"));

        let rows = visible_tree_rows(&roots, &children, &expanded);
        assert_eq!(rows[0].path, "/picked");
        assert_eq!(rows[0].depth, 0);
        assert_eq!(rows[1].depth, 1);
    }
}
