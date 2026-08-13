//! Expandable folder-tree rows under the current folder.

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
    let is_expanded = expanded.contains(path);
    rows.push(TreeRow {
        path: path.to_string(),
        depth,
        expanded: is_expanded,
        expandable: true,
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
        let roots = vec!["/root/a".into(), "/root/b".into()];
        let mut children = HashMap::new();
        apply_tree_children(
            &mut children,
            "/root/a",
            vec!["/root/a/one".into(), "/root/a/two".into()],
        );
        let mut expanded = HashSet::new();
        assert_eq!(toggle_expand(&mut expanded, "/root/a"), ExpandAction::NeedChildren);
        let rows = visible_tree_rows(&roots, &children, &expanded);
        let paths: Vec<&str> = rows.iter().map(|row| row.path.as_str()).collect();
        assert_eq!(
            paths,
            vec!["/root/a", "/root/a/one", "/root/a/two", "/root/b"]
        );
        assert_eq!(rows[1].depth, 1);
        assert_eq!(toggle_expand(&mut expanded, "/root/a"), ExpandAction::Collapse);
        let collapsed = visible_tree_rows(&roots, &children, &expanded);
        assert_eq!(collapsed.len(), 2);
    }
}
