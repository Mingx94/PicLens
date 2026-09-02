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

/// Replace the tree only for a picker selection or its startup restore.
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
    use super::*;

    #[test]
    fn root_is_fixed_while_descendants_expand_and_collapse() {
        let roots = vec!["/root".into()];
        let mut children = HashMap::new();
        apply_tree_children(&mut children, "/root", vec!["/root/a".into()]);
        apply_tree_children(&mut children, "/root/a", vec!["/root/a/one".into()]);
        let mut expanded = HashSet::new();

        let root_rows = visible_tree_rows(&roots, &children, &expanded);
        assert!(root_rows[0].expanded);
        assert!(!root_rows[0].expandable);

        assert_eq!(
            toggle_expand(&mut expanded, "/root/a"),
            ExpandAction::NeedChildren
        );
        assert_eq!(visible_tree_rows(&roots, &children, &expanded).len(), 3);
        assert_eq!(
            toggle_expand(&mut expanded, "/root/a"),
            ExpandAction::Collapse
        );
        assert_eq!(visible_tree_rows(&roots, &children, &expanded).len(), 2);
    }

    #[test]
    fn navigation_cannot_replace_the_picker_root() {
        let mut root = Some("/old".into());
        let mut roots = vec!["/old".into()];
        let mut children = HashMap::from([("/old".into(), vec!["/old/a".into()])]);
        let mut expanded = HashSet::from(["/old".into()]);

        assert!(!replace_tree_for_picker(
            false,
            &mut root,
            &mut roots,
            &mut children,
            &mut expanded,
            "/old/a",
            vec![],
        ));
        assert_eq!(root.as_deref(), Some("/old"));
        assert_eq!(roots, vec!["/old"]);

        assert!(replace_tree_for_picker(
            true,
            &mut root,
            &mut roots,
            &mut children,
            &mut expanded,
            "/new",
            vec!["/new/a".into()],
        ));
        assert_eq!(root.as_deref(), Some("/new"));
        assert_eq!(roots, vec!["/new"]);
    }
}
