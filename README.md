# Requirements

* .Net 7
* A Browser (Only tested in Chrome)

# Build & Run

1. Run the following git command to generate the heatdata file:
	* Nix systems
	`git log --since="2023-01-01" --name-only --pretty="format:" | sed '/^\s*$/'d | sort | uniq -c | sort -r > heatdata.txt`
	* Windows
	`(git log --since="2023-01-01" --name-only --pretty="format:") -notmatch '^\s*$' | Sort-Object | Group-Object | Select-Object | ForEach-Object {"     " + $_.Count + " " + $_.Name} | Sort-Object -Descending | Out-File heatdata.txt`
2. Navigate back this project folder and in powershell and run:
	`dotnet build`
3. Run the project with the following command:
	`dotnet run -- --path [path to scan folder] --heatdata [path to hotzone file from step 1]`
4. Open up the `dependentree.html` in your browser

# UI

By default the UI opens up in GraphView showing all projects found and their dependencies on other projects.

Each project node contains:
	1. On the left: Number of projects that depend on this project
	2. On the right: Number of projects that this project depends on
	3. In the center: Name of the project and a background that goes from White -> Red based on the number of changes that occured to it in the last 6 months.

In GraphView, the links from project to project are all in Grey.  In TreeView mode, they are in Green.  This is just to make things easier to see.

When selecting a node, the connetions will change colors:
	* Yellow:  Projects that Depend on this selected project
	* Magenta: Projects that the selected project depends on

When selecting a node, the dropdown at the bottom left of the screen will change to that selection.  You can also use the dropdown to find a particular project in the graph/tree.

Once you have a node selected, you can then press the TreeView button to narrow down the nodes shown to just the tree of dependencies for that project
