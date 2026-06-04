// ABOUTME: Declares stable editor command metadata used by shortcut help and future key binding persistence.
// ABOUTME: Keeps command ids, labels, default gestures, and scopes in one catalog instead of duplicating UI text.

namespace DBuilder.IO;

public enum EditorCommandScope
{
    Window,
    Map2D,
    Map3D,
}

public sealed record EditorCommandDescriptor(
    string Id,
    string Title,
    string DefaultGesture,
    EditorCommandScope Scope,
    bool AllowKeys = true,
    bool AllowMouse = true,
    bool AllowScroll = false,
    bool DisregardShift = false,
    bool DisregardAccelerator = false,
    bool DisregardAlt = false,
    bool Repeat = false);

public sealed record EditorShortcutBinding(
    string CommandId,
    EditorCommandScope Scope,
    string Key,
    bool Accelerator = false,
    bool Shift = false,
    bool Alt = false);

public static class EditorCommandCatalog
{
    public static IReadOnlyList<EditorCommandDescriptor> All { get; } = new[]
    {
        new EditorCommandDescriptor("window.undo", "Undo", "Ctrl/Cmd+Z", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.redo", "Redo", "Ctrl/Cmd+Y", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.new-map", "New Map", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.open-map", "Open Map", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.recover-autosave", "Recover Autosave", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.open-map-in-current-wad", "Open Map in current WAD", "Ctrl/Cmd+Shift+O", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.reload-map", "Reload Map", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.close-map", "Close Map", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.add-resource", "Add Resource", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.add-resource-directory", "Add Resource Directory", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.save", "Save", "Ctrl/Cmd+S", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.save-map", "Save Map", "Ctrl/Cmd+S", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.save-map-as", "Save Map As", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.save-as-format", "Save As Format", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.map-options", "Map Options", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.snap-selection-to-grid", "Snap Selected Map Elements to Grid", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.game-configurations", "Game Configurations", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.preferences", "Preferences", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.exit", "Exit", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.shortcuts", "Shortcuts", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.about", "About", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.view-used-tags", "View Used Tags", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.tag-explorer", "Tag Explorer", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.comments-panel", "Comments", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.view-thing-types", "View Thing Types", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.center-on-coordinates", "Go To Coordinates", "Ctrl/Cmd+Shift+G", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.go-to-coordinates", "Go To Coordinates", "Ctrl/Cmd+Shift+G", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.status-history", "Status History", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.show-errors", "Show Errors and Warnings", "F11", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.browse-wall-textures", "Browse Textures", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.browse-flats", "Browse Flats", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.browse-floor-flats", "Set Selected Floor Flats", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.browse-ceiling-flats", "Set Selected Ceiling Flats", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.browse-things", "Browse Things", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.browse-linedef-actions", "Browse Linedef Actions", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.browse-sector-effects", "Browse Sector Effects", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.model-render-none", "No Model Rendering", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.model-render-selection", "Model Rendering Selection Only", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.model-render-active-filter", "Model Rendering Active Things Filter Only", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.model-render-all", "Model Rendering All", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.next-model-render-mode", "Next Model Rendering Mode", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.toggle-3d-floors", "Show 3D Floors", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.toggle-blockmap", "Show Blockmap", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.toggle-nodes", "Show Nodes", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.toggle-info-panel", "Toggle Info Panel", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.cut", "Cut selection", "Ctrl/Cmd+X", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.copy", "Copy selection", "Ctrl/Cmd+C", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.paste", "Paste selection", "Ctrl/Cmd+V", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.paste-special", "Paste Selection Special", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.duplicate", "Duplicate selection", "Ctrl/Cmd+D", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.copy-properties", "Copy Properties", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.paste-properties", "Paste Properties", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.paste-properties-options", "Paste Properties With Options", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.delete", "Remove selection", "Delete", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-all", "Select all", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.invert-selection", "Invert selection", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-none", "Select none", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.properties", "Properties", "Enter", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.flags", "Flags", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.custom-fields", "Custom Fields", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.tags", "Tags", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-similar", "Select Similar Map Elements", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.filter-selected-things", "Filter Selected Things", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.change-map-element-index", "Change Map Element Index", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.stitch-geometry", "Stitch geometry", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.join-sectors", "Join sectors", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.merge-sectors", "Merge sectors", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.flip-selection-horizontal", "Flip Horizontal", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.flip-selection-vertical", "Flip Vertical", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.rotate-selection-cw", "Rotate 90 CW", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.rotate-selection-ccw", "Rotate 90 CCW", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.scale-selection-up", "Scale Up", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.scale-selection-down", "Scale Down", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.align-floor-to-front", "Align Floor to Front Side", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.align-floor-to-back", "Align Floor to Back Side", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.align-ceiling-to-front", "Align Ceiling to Front Side", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.align-ceiling-to-back", "Align Ceiling to Back Side", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.build-bridge", "Build Bridge", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.make-door", "Make Door", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.build-stairs", "Build Stairs", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.tag-range", "Tag Range", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.blockmap-explorer", "Blockmap Explorer mode", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.reject-explorer", "Reject Explorer mode", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.nodes-viewer", "Nodes Viewer Mode", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.sound-propagation-mode", "Sound propagation mode", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.sound-environment-mode", "Sound environment mode", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.sound-propagation-colors", "Configure colors", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.apply-slope-arch", "Apply Slope Arch", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.apply-slopes", "Apply Slopes", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-floor-heights", "Gradient Floor Heights", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-ceiling-heights", "Gradient Ceiling Heights", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-sector-brightness", "Gradient Brightness", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-floor-light", "Gradient Floor Light", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-ceiling-light", "Gradient Ceiling Light", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-light-color", "Gradient Light Color", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-fade-color", "Gradient Fade Color", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-light-and-fade-colors", "Gradient Light and Fade Colors", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-linedef-brightness", "Gradient Linedef Brightness", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-interpolation-linear", "Gradient Interpolation Linear", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-interpolation-ease-in-out-sine", "Gradient Interpolation Ease In/Out Sine", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-interpolation-ease-in-sine", "Gradient Interpolation Ease In Sine", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.gradient-interpolation-ease-out-sine", "Gradient Interpolation Ease Out Sine", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.toggle-automap-secret-line", "Toggle Selected Line Secret", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.toggle-automap-hidden-line", "Toggle Selected Line Hidden", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.toggle-automap-textured-hidden-sector", "Toggle Selected Sector Textured Hidden", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.sector-color", "Sector Color", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.dynamic-light-color", "Dynamic Light Color", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.cancel-draw", "Cancel draw mode", "Esc", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.toggle-auto-clear-sidedef-textures", "Auto Clear Sidedef Textures", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.undo-redo-panel", "Undo / Redo Panel", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.check-map", "Check Map", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.clean-up-geometry", "Clean Up Geometry", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.test-map", "Test Map", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.things-filters-setup", "Configure Things Filters", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.reload-resources", "Reload Resources", "Menu", EditorCommandScope.Window, AllowMouse: false),
        new EditorCommandDescriptor("window.grid-setup", "Grid and Backdrop Setup", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.usdf-conversations", "USDF Conversations", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.usdf-dialog-editor", "Dialog Editor", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.import-obj-terrain", "Import OBJ Terrain", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.export-object", "Export Object OBJ", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.export-image", "Export Image PNG", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.export-wavefront", "Export Wavefront OBJ", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.export-idstudio", "Export idStudio", "Menu", EditorCommandScope.Window, AllowScroll: true),
        new EditorCommandDescriptor("window.create-prefab", "Create Prefab", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.insert-prefab-file", "Insert Prefab File", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.insert-previous-prefab", "Insert Previous Prefab", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.align-things-to-wall", "Align Things to Wall", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.find-replace", "Find and Replace", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-group-1", "Select Group 1", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-group-2", "Select Group 2", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-group-3", "Select Group 3", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-group-4", "Select Group 4", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-group-5", "Select Group 5", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-group-6", "Select Group 6", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-group-7", "Select Group 7", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-group-8", "Select Group 8", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-group-9", "Select Group 9", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.select-group-10", "Select Group 10", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.assign-group-1", "Assign Group 1", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.assign-group-2", "Assign Group 2", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.assign-group-3", "Assign Group 3", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.assign-group-4", "Assign Group 4", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.assign-group-5", "Assign Group 5", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.assign-group-6", "Assign Group 6", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.assign-group-7", "Assign Group 7", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.assign-group-8", "Assign Group 8", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.assign-group-9", "Assign Group 9", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.assign-group-10", "Assign Group 10", "Menu", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.clear-group-1", "Clear Group 1", "Ctrl/Cmd+Shift+1", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.clear-group-2", "Clear Group 2", "Ctrl/Cmd+Shift+2", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.clear-group-3", "Clear Group 3", "Ctrl/Cmd+Shift+3", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.clear-group-4", "Clear Group 4", "Ctrl/Cmd+Shift+4", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.clear-group-5", "Clear Group 5", "Ctrl/Cmd+Shift+5", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.clear-group-6", "Clear Group 6", "Ctrl/Cmd+Shift+6", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.clear-group-7", "Clear Group 7", "Ctrl/Cmd+Shift+7", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.clear-group-8", "Clear Group 8", "Ctrl/Cmd+Shift+8", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.clear-group-9", "Clear Group 9", "Ctrl/Cmd+Shift+9", EditorCommandScope.Window),
        new EditorCommandDescriptor("window.clear-group-10", "Clear Group 10", "Ctrl/Cmd+Shift+0", EditorCommandScope.Window),

        new EditorCommandDescriptor("map2d.select", "Select element", "Click", EditorCommandScope.Map2D, DisregardShift: true, DisregardAccelerator: true, DisregardAlt: true),
        new EditorCommandDescriptor("map2d.box-select", "Box-select or move a grabbed vertex/thing", "Left-drag", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.pan", "Pan the view", "Right-drag", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.zoom", "Zoom out / in", "Wheel / - =", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.fit", "Fit map to view", "R", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.edit-properties", "Edit properties", "Double-click", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.split-line", "Split Linedefs", "Right-click", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.zoom-in", "Zoom in", "+", EditorCommandScope.Map2D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map2d.zoom-out", "Zoom out", "-", EditorCommandScope.Map2D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map2d.toggle-event-lines", "Toggle Event lines", "Menu", EditorCommandScope.Map2D, AllowMouse: false),
        new EditorCommandDescriptor("map2d.mode-vertices", "Vertices mode", "1", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.mode-linedefs", "Linedefs mode", "2", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.mode-sectors", "Sectors mode", "3", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.mode-things", "Things mode", "4", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.mode-image-example", "Image Example", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.mode-automap", "Automap Mode", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.mode-wadauthor", "WadAuthor Mode", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.mode-visplane-explorer", "Visplane Explorer Mode", "Menu", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.mode-stair-sector-builder", "Stair Sector Builder Mode", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.select-sectors-outline", "Select Sectors Outline", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.mode-3d-floor", "3D Floor Mode", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.mode-3d-slope", "Slope Mode", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.mode-draw-slopes", "Draw Slopes Mode", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.bridge-mode", "Bridge Mode", "Ctrl/Cmd+B", EditorCommandScope.Map2D, AllowMouse: false),
        new EditorCommandDescriptor("map2d.3dfloor.draw-slope-point", "Draw slope vertex", "LButton", EditorCommandScope.Map2D, AllowScroll: true, DisregardShift: true, DisregardAccelerator: true),
        new EditorCommandDescriptor("map2d.3dfloor.draw-floor-slope", "Draw Floor Slope", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.3dfloor.draw-ceiling-slope", "Draw Ceiling Slope", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.3dfloor.draw-floor-and-ceiling-slope", "Draw Floor and Ceiling Slope", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.3dfloor.finish-slope-draw", "Finish Slope Drawing", "RButton", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.3dfloor.flip-slope", "Flip 3D slope", "Menu", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.3dfloor.cycle-highlight-up", "Cycle highlighted 3D floor up", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.3dfloor.cycle-highlight-down", "Cycle highlighted 3D floor down", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.3dfloor.relocate-control-sectors", "Relocate 3D floor control sectors", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.3dfloor.select-control-sector", "Select 3D floor control sector", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.3dfloor.duplicate-geometry", "Duplicate and paste geometry", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.toggle-sector-fills", "Toggle sector fills", "S", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.toggle-things", "Toggle things", "T", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.toggle-thing-arrows", "Toggle sprites / direction arrows", "Y", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.toggle-comments", "Toggle Comments", "Menu", EditorCommandScope.Map2D, AllowMouse: false),
        new EditorCommandDescriptor("map2d.toggle-fixed-things-scale", "Toggle Fixed Things Scale", "Menu", EditorCommandScope.Map2D, AllowMouse: false),
        new EditorCommandDescriptor("map2d.toggle-always-show-vertices", "Toggle Always Show Vertices", "Menu", EditorCommandScope.Map2D, AllowMouse: false),
        new EditorCommandDescriptor("map2d.toggle-full-brightness", "Toggle Full Brightness", "B", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.toggle-highlight", "Toggle Highlight", "H", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.view-mode-wireframe", "View Wireframe", "Menu", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.view-mode-brightness", "View Brightness Levels", "Menu", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.view-mode-floors", "View Floor Textures", "Menu", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.view-mode-ceilings", "View Ceiling Textures", "Menu", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.next-view-mode", "Next View Mode", "Menu", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.previous-view-mode", "Previous View Mode", "Menu", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.draw-sector", "Draw sector", "D", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.draw-lines", "Draw lines", "Shift+D", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.draw-rectangle", "Start Rectangle Drawing", "Ctrl/Cmd+Shift+D", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.draw-ellipse", "Start Ellipse Drawing", "Alt+Shift+D", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.draw-curve", "Start Curve Drawing", "Ctrl/Cmd+Alt+D", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.draw-grid", "Start Grid Drawing", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.increase-subdivision-level", "Increase Subdivision Level", "Ctrl/Cmd+ScrollUp", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.decrease-subdivision-level", "Decrease Subdivision Level", "Ctrl/Cmd+ScrollDown", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.increase-bevel", "Increase Corners Bevel", "Ctrl/Cmd+Shift+ScrollUp", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.decrease-bevel", "Decrease Corners Bevel", "Ctrl/Cmd+Shift+ScrollDown", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.draw-point", "Draw Vertex", "Menu", EditorCommandScope.Map2D, AllowScroll: true, DisregardShift: true, DisregardAccelerator: true, DisregardAlt: true),
        new EditorCommandDescriptor("map2d.remove-draw-point", "Remove Last Vertex", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.remove-first-draw-point", "Remove First Vertex", "Ctrl/Cmd+Backspace", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.insert", "Insert vertex or thing", "I", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.place-things", "Place Things", "Menu", EditorCommandScope.Map2D, AllowMouse: false),
        new EditorCommandDescriptor("map2d.point-thing-to-cursor", "Point Thing to Cursor", "Shift+L", EditorCommandScope.Map2D, AllowScroll: true, DisregardAccelerator: true),
        new EditorCommandDescriptor("map2d.make-sector", "Make sector at cursor", "M", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.flip", "Flip Linedefs", "F", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.flip-sidedefs", "Flip Sidedefs", "Shift+F", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.select-single-sided", "Select Single-sided", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.select-double-sided", "Select Double-sided", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.align-linedefs", "Align Linedefs", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.split-linedefs", "Split Linedefs", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.join-sectors", "Join Sectors", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.merge-sectors", "Merge Sectors", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.lower-floor-8", "Lower Floor by 8 mp", "Menu", EditorCommandScope.Map2D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map2d.raise-floor-8", "Raise Floor by 8 mp", "Menu", EditorCommandScope.Map2D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map2d.lower-ceiling-8", "Lower Ceiling by 8 mp", "Menu", EditorCommandScope.Map2D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map2d.raise-ceiling-8", "Raise Ceiling by 8 mp", "Menu", EditorCommandScope.Map2D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map2d.raise-brightness-8", "Increase Brightness by 8", "Menu", EditorCommandScope.Map2D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map2d.lower-brightness-8", "Decrease Brightness by 8", "Menu", EditorCommandScope.Map2D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map2d.align-textures-x", "Align textures X", "A", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.align-textures-y", "Align textures Y", "Shift+A", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.fit-selected-textures", "Fit Selected Textures", "Menu", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.apply-lightfog-flag", "Apply 'lightfog' flag", "Menu", EditorCommandScope.Map2D, AllowMouse: false),
        new EditorCommandDescriptor("map2d.toggle-grid-snap", "Toggle grid snap", "G", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.toggle-dynamic-grid-size", "Toggle Dynamic Grid Size", "Ctrl/Cmd+Alt+G", EditorCommandScope.Map2D, AllowMouse: false),
        new EditorCommandDescriptor("map2d.align-grid-to-linedef", "Align Grid to Selected Linedef", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.set-grid-origin-to-vertex", "Set Grid Origin to Selected Vertex", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.reset-grid-transform", "Reset Grid Transform", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.smart-grid-transform", "Smart Grid Transform", "Menu", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.grid-down", "Decrease grid size", "[", EditorCommandScope.Map2D, Repeat: true),
        new EditorCommandDescriptor("map2d.grid-up", "Increase grid size", "]", EditorCommandScope.Map2D, Repeat: true),
        new EditorCommandDescriptor("map2d.finish-draw", "Finish Drawing", "Enter", EditorCommandScope.Map2D, AllowScroll: true),
        new EditorCommandDescriptor("map2d.cancel-draw", "Cancel drawing", "Esc", EditorCommandScope.Map2D),
        new EditorCommandDescriptor("map2d.toggle-3d", "Enter 3D mode", "Tab", EditorCommandScope.Map2D),

        new EditorCommandDescriptor("map3d.move", "Move", "WASD", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.move-forward", "Move Forward", "Menu", EditorCommandScope.Map3D, AllowScroll: true, DisregardShift: true),
        new EditorCommandDescriptor("map3d.move-backward", "Move Backward", "Menu", EditorCommandScope.Map3D, AllowScroll: true, DisregardShift: true),
        new EditorCommandDescriptor("map3d.move-left", "Move Left (strafe)", "Menu", EditorCommandScope.Map3D, AllowScroll: true, DisregardShift: true),
        new EditorCommandDescriptor("map3d.move-right", "Move Right (strafe)", "Menu", EditorCommandScope.Map3D, AllowScroll: true, DisregardShift: true),
        new EditorCommandDescriptor("map3d.orbit", "Orbit", "Menu", EditorCommandScope.Map3D, DisregardShift: true),
        new EditorCommandDescriptor("map3d.look", "Look around", "Arrows / drag", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.move-up", "Move Up", "Menu", EditorCommandScope.Map3D, AllowScroll: true, DisregardShift: true),
        new EditorCommandDescriptor("map3d.move-down", "Move Down", "Menu", EditorCommandScope.Map3D, AllowScroll: true, DisregardShift: true),
        new EditorCommandDescriptor("map3d.move-height", "Move up / down", "Q / E", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.toggle-gravity", "Toggle Gravity", "G", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.walk-mode", "Toggle walk mode (gravity)", "G", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.move-camera-to-cursor", "Move Camera to Cursor", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.move-thing-left", "Move Thing Left", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-thing-right", "Move Thing Right", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-thing-forward", "Move Thing Forward", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-thing-backward", "Move Thing Backward", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.insert-item", "Insert Item", "I / Insert", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.copy-selection", "Copy Selection", "Ctrl/Cmd+C", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.cut-selection", "Cut Selection", "Ctrl/Cmd+X", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.paste-selection", "Paste Selection", "Ctrl/Cmd+V", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.place-thing-at-cursor", "Move Thing to Cursor Location", "Menu", EditorCommandScope.Map3D, AllowScroll: false),
        new EditorCommandDescriptor("map3d.rotate-clockwise", "Rotate Clockwise", "Ctrl/Cmd+Shift+ScrollUp", EditorCommandScope.Map3D, AllowMouse: false, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.rotate-thing-clockwise", "Rotate Thing Clockwise", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.rotate-counterclockwise", "Rotate Counterclockwise", "Ctrl/Cmd+Shift+ScrollDown", EditorCommandScope.Map3D, AllowMouse: false, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.rotate-thing-counterclockwise", "Rotate Thing Counter-clockwise", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.pitch-clockwise", "Change Pitch Clockwise", "Ctrl/Cmd+Alt+ScrollUp", EditorCommandScope.Map3D, AllowMouse: false, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.pitch-thing-clockwise", "Pitch Thing Clockwise", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.pitch-counterclockwise", "Change Pitch Counterclockwise", "Ctrl/Cmd+Alt+ScrollDown", EditorCommandScope.Map3D, AllowMouse: false, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.pitch-thing-counterclockwise", "Pitch Thing Counter-clockwise", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.roll-clockwise", "Change Roll Clockwise", "Alt+ScrollUp", EditorCommandScope.Map3D, AllowMouse: false, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.roll-thing-clockwise", "Roll Thing Clockwise", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.roll-counterclockwise", "Change Roll Counterclockwise", "Alt+ScrollDown", EditorCommandScope.Map3D, AllowMouse: false, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.roll-thing-counterclockwise", "Roll Thing Counter-clockwise", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.apply-camera-rotation", "Apply Camera Rotation To Things", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.apply-camera-rotation-to-things", "Apply Camera Rotation To Things", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.look-through-selection", "Look Through Selection", "Y", EditorCommandScope.Map3D, AllowMouse: false, AllowScroll: true),
        new EditorCommandDescriptor("map3d.look-through-thing", "Look Through Selection", "Y", EditorCommandScope.Map3D, AllowMouse: false, AllowScroll: true),
        new EditorCommandDescriptor("map3d.thing-align-to-wall", "Align Things to Nearest Linedef", "Ctrl/Cmd+Shift+A", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.align-things-to-wall", "Align Things to Nearest Linedef", "Ctrl/Cmd+Shift+A", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.show-visual-things", "Show Things", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.scale-up", "Increase Scale", "NumPad9", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.scale-down", "Decrease Scale", "NumPad7", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.scale-up-x", "Increase Horizontal Scale", "NumPad6", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.scale-down-x", "Decrease Horizontal Scale", "NumPad4", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.scale-up-y", "Increase Vertical Scale", "NumPad8", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.scale-down-y", "Decrease Vertical Scale", "NumPad5", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.target-height", "Raise/lower floor, ceiling or thing Z (Shift = by 1)", "Wheel", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.drag-height", "Move a thing or drag a surface height", "Right-drag", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.lower-sector-1", "Lower Floor/Ceiling/Thing by 1 mp", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.raise-sector-1", "Raise Floor/Ceiling/Thing by 1 mp", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.lower-sector-8", "Lower Floor/Ceiling/Thing by 8 mp", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.raise-sector-8", "Raise Floor/Ceiling/Thing by 8 mp", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.lower-sector-128", "Lower Floor/Ceiling/Thing by 128 mp", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.raise-sector-128", "Raise Floor/Ceiling/Thing by 128 mp", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.lower-map-element-by-grid-size", "Lower Floor/Ceiling/Thing by grid size", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.raise-map-element-by-grid-size", "Raise Floor/Ceiling/Thing by grid size", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.lower-sector-to-nearest", "Lower Floor/Ceiling/Thing to adjacent Sector/Thing", "PageDown", EditorCommandScope.Map3D, AllowScroll: true, DisregardAccelerator: true),
        new EditorCommandDescriptor("map3d.raise-sector-to-nearest", "Raise Floor/Ceiling/Thing to adjacent Sector/Thing", "PageUp", EditorCommandScope.Map3D, AllowScroll: true, DisregardAccelerator: true),
        new EditorCommandDescriptor("map3d.raise-brightness-8", "Increase Brightness by 8", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.lower-brightness-8", "Decrease Brightness by 8", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.match-brightness", "Match Brightness", "Ctrl/Cmd+M", EditorCommandScope.Map3D, AllowMouse: false, AllowScroll: false),
        new EditorCommandDescriptor("map3d.brightness-down", "Sector brightness down", "[", EditorCommandScope.Map3D, Repeat: true),
        new EditorCommandDescriptor("map3d.brightness-up", "Sector brightness up", "]", EditorCommandScope.Map3D, Repeat: true),
        new EditorCommandDescriptor("map3d.texture-copy", "Copy Texture", "C", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.copy-texture", "Copy Texture", "C", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.texture-paste", "Paste Texture", "V", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.apply-texture", "Paste Texture", "V", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.flood-fill-texture", "Paste Texture Flood-Fill", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.select-texture", "Select Texture", "T", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.browse-texture", "Select Texture", "T", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.align-texture-x", "Align texture X", "A", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.align-texture-y", "Align texture Y", "Shift+A", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.visual-auto-align", "Auto-align Textures X and Y", "Ctrl+A", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.visual-auto-align-x", "Auto-align Textures X", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.visual-auto-align-y", "Auto-align Textures Y", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.visual-auto-align-to-selection", "Auto-align Textures to Selection (X and Y)", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.visual-auto-align-to-selection-x", "Auto-align Textures to Selection (X)", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.visual-auto-align-to-selection-y", "Auto-align Textures to Selection (Y)", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.nudge-offset", "Nudge texture offset", "Shift+arrows", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.nudge-offset-left", "Nudge texture offset left", "Shift+Left", EditorCommandScope.Map3D, Repeat: true),
        new EditorCommandDescriptor("map3d.nudge-offset-right", "Nudge texture offset right", "Shift+Right", EditorCommandScope.Map3D, Repeat: true),
        new EditorCommandDescriptor("map3d.nudge-offset-up", "Nudge texture offset up", "Shift+Up", EditorCommandScope.Map3D, Repeat: true),
        new EditorCommandDescriptor("map3d.nudge-offset-down", "Nudge texture offset down", "Shift+Down", EditorCommandScope.Map3D, Repeat: true),
        new EditorCommandDescriptor("map3d.move-texture-left-1", "Move Texture Left by 1", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-texture-right-1", "Move Texture Right by 1", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-texture-up-1", "Move Texture Up by 1", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-texture-down-1", "Move Texture Down by 1", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-texture-left-8", "Move Texture Left by 8", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-texture-right-8", "Move Texture Right by 8", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-texture-up-8", "Move Texture Up by 8", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-texture-down-8", "Move Texture Down by 8", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-texture-left-grid", "Move Texture Left by Grid Size", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-texture-right-grid", "Move Texture Right by Grid Size", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-texture-up-grid", "Move Texture Up by Grid Size", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.move-texture-down-grid", "Move Texture Down by Grid Size", "Menu", EditorCommandScope.Map3D, AllowScroll: true, Repeat: true),
        new EditorCommandDescriptor("map3d.reset-offsets", "Reset Texture Offsets", "O", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.reset-local-offsets", "Reset Local Texture Offsets (UDMF)", "Ctrl/Cmd+Shift+R", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.texture-copy-offsets", "Copy Offsets", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.copy-offsets", "Copy Offsets", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.texture-paste-offsets", "Paste Offsets", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.paste-offsets", "Paste Offsets", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.copy-properties", "Copy Properties", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.paste-properties", "Paste Properties", "Ctrl/Cmd+Alt+V", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.paste-properties-options", "Paste Properties Special", "Ctrl/Cmd+Shift+V", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.fit-textures", "Fit Textures", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.toggle-upper-unpegged", "Toggle Upper Unpegged", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.toggle-lower-unpegged", "Toggle Lower Unpegged", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.toggle-slope", "Toggle Slope", "Alt+S", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.toggle-models-rendering", "Toggle models rendering", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.toggle-model-rendering", "Toggle Model Rendering Mode", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.toggle-dynamic-lights-rendering", "Toggle dynamic lights rendering", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.toggle-enhanced-rendering-effects", "Toggle Enhanced Rendering Effects", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.toggle-classic-rendering", "Toggle classic rendering", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.toggle-fog-rendering", "Toggle fog rendering", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.toggle-sky-rendering", "Toggle sky rendering", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.toggle-event-lines", "Toggle Event lines", "Menu", EditorCommandScope.Map3D, AllowMouse: false),
        new EditorCommandDescriptor("map3d.toggle-visual-vertices", "Toggle Visual Vertices", "Alt+V", EditorCommandScope.Map3D, AllowMouse: false),
        new EditorCommandDescriptor("map3d.toggle-alpha-based-texture-highlighting", "Toggle Alpha-based Texture Highlighting", "Menu", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.toggle-visual-sidedef-slope-picking", "Toggle Visual Sidedef Slope Picking", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.toggle-visual-vertex-slope-picking", "Toggle Visual Vertex Slope Picking", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.toggle-visual-vertex-slope-adjacent-selection", "Toggle Adjacent Visual Vertex Slope Selection", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.reset-slope", "Reset Plane Slope", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.slope-between-handles", "Slope Between Handles", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.arch-between-handles", "Arch Between Slope Handles", "Menu", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.toggle-full-brightness", "Toggle Full Brightness", "B", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.toggle-highlight", "Toggle Highlight", "H", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.visual-select", "Select", "Click", EditorCommandScope.Map3D, DisregardShift: true, DisregardAccelerator: true, DisregardAlt: true),
        new EditorCommandDescriptor("map3d.visual-paint-select", "Paint Selection", "Menu", EditorCommandScope.Map3D, AllowScroll: false, DisregardShift: true, DisregardAccelerator: true, DisregardAlt: true),
        new EditorCommandDescriptor("map3d.select-target", "Select surfaces", "Click", EditorCommandScope.Map3D, DisregardShift: true, DisregardAccelerator: true, DisregardAlt: true),
        new EditorCommandDescriptor("map3d.clear-selection", "Clear Selection", "Esc", EditorCommandScope.Map3D, AllowScroll: true),
        new EditorCommandDescriptor("map3d.clear-target", "Clear selection", "Esc", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.visual-edit", "Edit", "Enter", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.edit-properties", "Edit properties", "Enter", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.delete-target", "Delete Item", "Delete", EditorCommandScope.Map3D),
        new EditorCommandDescriptor("map3d.toggle-2d", "Back to 2D mode", "Tab", EditorCommandScope.Map3D),
    }.Concat(UdbScriptActions.CommandDescriptors).ToArray();

    public static IReadOnlyList<EditorShortcutBinding> DefaultShortcuts { get; } = new[]
    {
        new EditorShortcutBinding("window.undo", EditorCommandScope.Window, "Z", Accelerator: true),
        new EditorShortcutBinding("window.redo", EditorCommandScope.Window, "Y", Accelerator: true),
        new EditorShortcutBinding("window.save", EditorCommandScope.Window, "S", Accelerator: true),
        new EditorShortcutBinding("window.open-map-in-current-wad", EditorCommandScope.Window, "O", Accelerator: true, Shift: true),
        new EditorShortcutBinding("window.center-on-coordinates", EditorCommandScope.Window, "G", Accelerator: true, Shift: true),
        new EditorShortcutBinding("window.show-errors", EditorCommandScope.Window, "F11"),
        new EditorShortcutBinding("window.cut", EditorCommandScope.Window, "X", Accelerator: true),
        new EditorShortcutBinding("window.copy", EditorCommandScope.Window, "C", Accelerator: true),
        new EditorShortcutBinding("window.paste", EditorCommandScope.Window, "V", Accelerator: true),
        new EditorShortcutBinding("window.duplicate", EditorCommandScope.Window, "D", Accelerator: true),
        new EditorShortcutBinding("window.delete", EditorCommandScope.Window, "Delete"),
        new EditorShortcutBinding("window.delete", EditorCommandScope.Window, "Back"),
        new EditorShortcutBinding("window.properties", EditorCommandScope.Window, "Enter"),
        new EditorShortcutBinding("window.cancel-draw", EditorCommandScope.Window, "Escape"),
        new EditorShortcutBinding("window.clear-group-1", EditorCommandScope.Window, "D1", Accelerator: true, Shift: true),
        new EditorShortcutBinding("window.clear-group-2", EditorCommandScope.Window, "D2", Accelerator: true, Shift: true),
        new EditorShortcutBinding("window.clear-group-3", EditorCommandScope.Window, "D3", Accelerator: true, Shift: true),
        new EditorShortcutBinding("window.clear-group-4", EditorCommandScope.Window, "D4", Accelerator: true, Shift: true),
        new EditorShortcutBinding("window.clear-group-5", EditorCommandScope.Window, "D5", Accelerator: true, Shift: true),
        new EditorShortcutBinding("window.clear-group-6", EditorCommandScope.Window, "D6", Accelerator: true, Shift: true),
        new EditorShortcutBinding("window.clear-group-7", EditorCommandScope.Window, "D7", Accelerator: true, Shift: true),
        new EditorShortcutBinding("window.clear-group-8", EditorCommandScope.Window, "D8", Accelerator: true, Shift: true),
        new EditorShortcutBinding("window.clear-group-9", EditorCommandScope.Window, "D9", Accelerator: true, Shift: true),
        new EditorShortcutBinding("window.clear-group-10", EditorCommandScope.Window, "D0", Accelerator: true, Shift: true),

        new EditorShortcutBinding("map2d.select", EditorCommandScope.Map2D, EditorPointerInput.LeftButton),
        new EditorShortcutBinding("map2d.split-line", EditorCommandScope.Map2D, EditorPointerInput.RightButton),
        new EditorShortcutBinding("map2d.toggle-sector-fills", EditorCommandScope.Map2D, "S"),
        new EditorShortcutBinding("map2d.toggle-full-brightness", EditorCommandScope.Map2D, "B"),
        new EditorShortcutBinding("map2d.toggle-highlight", EditorCommandScope.Map2D, "H"),
        new EditorShortcutBinding("map2d.toggle-things", EditorCommandScope.Map2D, "T"),
        new EditorShortcutBinding("map2d.toggle-thing-arrows", EditorCommandScope.Map2D, "Y"),
        new EditorShortcutBinding("map2d.draw-sector", EditorCommandScope.Map2D, "D"),
        new EditorShortcutBinding("map2d.draw-lines", EditorCommandScope.Map2D, "D", Shift: true),
        new EditorShortcutBinding("map2d.draw-rectangle", EditorCommandScope.Map2D, "D", Accelerator: true, Shift: true),
        new EditorShortcutBinding("map2d.draw-ellipse", EditorCommandScope.Map2D, "D", Alt: true, Shift: true),
        new EditorShortcutBinding("map2d.draw-curve", EditorCommandScope.Map2D, "D", Accelerator: true, Alt: true),
        new EditorShortcutBinding("map2d.bridge-mode", EditorCommandScope.Map2D, "B", Accelerator: true),
        new EditorShortcutBinding("map2d.increase-subdivision-level", EditorCommandScope.Map2D, EditorPointerInput.ScrollUp, Accelerator: true),
        new EditorShortcutBinding("map2d.decrease-subdivision-level", EditorCommandScope.Map2D, EditorPointerInput.ScrollDown, Accelerator: true),
        new EditorShortcutBinding("map2d.increase-bevel", EditorCommandScope.Map2D, EditorPointerInput.ScrollUp, Accelerator: true, Shift: true),
        new EditorShortcutBinding("map2d.decrease-bevel", EditorCommandScope.Map2D, EditorPointerInput.ScrollDown, Accelerator: true, Shift: true),
        new EditorShortcutBinding("map2d.remove-first-draw-point", EditorCommandScope.Map2D, "Back", Accelerator: true),
        new EditorShortcutBinding("map2d.make-sector", EditorCommandScope.Map2D, "M"),
        new EditorShortcutBinding("map2d.insert", EditorCommandScope.Map2D, "I"),
        new EditorShortcutBinding("map2d.insert", EditorCommandScope.Map2D, "Insert"),
        new EditorShortcutBinding("map2d.point-thing-to-cursor", EditorCommandScope.Map2D, "L", Shift: true),
        new EditorShortcutBinding("map2d.mode-vertices", EditorCommandScope.Map2D, "D1"),
        new EditorShortcutBinding("map2d.mode-vertices", EditorCommandScope.Map2D, "NumPad1"),
        new EditorShortcutBinding("map2d.mode-linedefs", EditorCommandScope.Map2D, "D2"),
        new EditorShortcutBinding("map2d.mode-linedefs", EditorCommandScope.Map2D, "NumPad2"),
        new EditorShortcutBinding("map2d.mode-sectors", EditorCommandScope.Map2D, "D3"),
        new EditorShortcutBinding("map2d.mode-sectors", EditorCommandScope.Map2D, "NumPad3"),
        new EditorShortcutBinding("map2d.mode-things", EditorCommandScope.Map2D, "D4"),
        new EditorShortcutBinding("map2d.mode-things", EditorCommandScope.Map2D, "NumPad4"),
        new EditorShortcutBinding("map2d.flip", EditorCommandScope.Map2D, "F"),
        new EditorShortcutBinding("map2d.flip-sidedefs", EditorCommandScope.Map2D, "F", Shift: true),
        new EditorShortcutBinding("map2d.align-textures-x", EditorCommandScope.Map2D, "A"),
        new EditorShortcutBinding("map2d.align-textures-y", EditorCommandScope.Map2D, "A", Shift: true),
        new EditorShortcutBinding("map2d.toggle-grid-snap", EditorCommandScope.Map2D, "G"),
        new EditorShortcutBinding("map2d.toggle-dynamic-grid-size", EditorCommandScope.Map2D, "G", Accelerator: true, Alt: true),
        new EditorShortcutBinding("map2d.grid-down", EditorCommandScope.Map2D, "OemOpenBrackets"),
        new EditorShortcutBinding("map2d.grid-up", EditorCommandScope.Map2D, "OemCloseBrackets"),
        new EditorShortcutBinding("map2d.finish-draw", EditorCommandScope.Map2D, "Enter"),
        new EditorShortcutBinding("map2d.cancel-draw", EditorCommandScope.Map2D, "Escape"),
        new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "R"),
        new EditorShortcutBinding("map2d.zoom-in", EditorCommandScope.Map2D, "OemPlus"),
        new EditorShortcutBinding("map2d.zoom-in", EditorCommandScope.Map2D, "Add"),
        new EditorShortcutBinding("map2d.zoom-in", EditorCommandScope.Map2D, EditorPointerInput.ScrollUp),
        new EditorShortcutBinding("map2d.zoom-out", EditorCommandScope.Map2D, "OemMinus"),
        new EditorShortcutBinding("map2d.zoom-out", EditorCommandScope.Map2D, "Subtract"),
        new EditorShortcutBinding("map2d.zoom-out", EditorCommandScope.Map2D, EditorPointerInput.ScrollDown),
        new EditorShortcutBinding("map2d.toggle-3d", EditorCommandScope.Map2D, "Tab"),
        new EditorShortcutBinding("map3d.toggle-2d", EditorCommandScope.Map3D, "Tab"),
        new EditorShortcutBinding("map3d.toggle-gravity", EditorCommandScope.Map3D, "G"),
        new EditorShortcutBinding("map3d.insert-item", EditorCommandScope.Map3D, "I"),
        new EditorShortcutBinding("map3d.insert-item", EditorCommandScope.Map3D, "Insert"),
        new EditorShortcutBinding("map3d.cut-selection", EditorCommandScope.Map3D, "X", Accelerator: true),
        new EditorShortcutBinding("map3d.copy-selection", EditorCommandScope.Map3D, "C", Accelerator: true),
        new EditorShortcutBinding("map3d.paste-selection", EditorCommandScope.Map3D, "V", Accelerator: true),
        new EditorShortcutBinding("map3d.lower-brightness-8", EditorCommandScope.Map3D, "OemOpenBrackets"),
        new EditorShortcutBinding("map3d.raise-brightness-8", EditorCommandScope.Map3D, "OemCloseBrackets"),
        new EditorShortcutBinding("map3d.match-brightness", EditorCommandScope.Map3D, "M", Accelerator: true),
        new EditorShortcutBinding("map3d.lower-sector-to-nearest", EditorCommandScope.Map3D, "PageDown"),
        new EditorShortcutBinding("map3d.raise-sector-to-nearest", EditorCommandScope.Map3D, "PageUp"),
        new EditorShortcutBinding("map3d.toggle-full-brightness", EditorCommandScope.Map3D, "B"),
        new EditorShortcutBinding("map3d.toggle-highlight", EditorCommandScope.Map3D, "H"),
        new EditorShortcutBinding("map3d.toggle-visual-vertices", EditorCommandScope.Map3D, "V", Alt: true),
        new EditorShortcutBinding("map3d.texture-copy", EditorCommandScope.Map3D, "C"),
        new EditorShortcutBinding("map3d.texture-paste", EditorCommandScope.Map3D, "V"),
        new EditorShortcutBinding("map3d.paste-properties", EditorCommandScope.Map3D, "V", Accelerator: true, Alt: true),
        new EditorShortcutBinding("map3d.paste-properties-options", EditorCommandScope.Map3D, "V", Accelerator: true, Shift: true),
        new EditorShortcutBinding("map3d.visual-auto-align", EditorCommandScope.Map3D, "A", Accelerator: true),
        new EditorShortcutBinding("map3d.align-texture-x", EditorCommandScope.Map3D, "A"),
        new EditorShortcutBinding("map3d.align-texture-y", EditorCommandScope.Map3D, "A", Shift: true),
        new EditorShortcutBinding("map3d.visual-edit", EditorCommandScope.Map3D, "Enter"),
        new EditorShortcutBinding("map3d.clear-selection", EditorCommandScope.Map3D, "Escape"),
        new EditorShortcutBinding("map3d.reset-offsets", EditorCommandScope.Map3D, "O"),
        new EditorShortcutBinding("map3d.reset-local-offsets", EditorCommandScope.Map3D, "R", Accelerator: true, Shift: true),
        new EditorShortcutBinding("map3d.delete-target", EditorCommandScope.Map3D, "Delete"),
        new EditorShortcutBinding("map3d.delete-target", EditorCommandScope.Map3D, "Back"),
        new EditorShortcutBinding("map3d.toggle-slope", EditorCommandScope.Map3D, "S", Alt: true),
        new EditorShortcutBinding("map3d.visual-select", EditorCommandScope.Map3D, EditorPointerInput.LeftButton),
        new EditorShortcutBinding("map3d.select-texture", EditorCommandScope.Map3D, "T"),
        new EditorShortcutBinding("map3d.look-through-selection", EditorCommandScope.Map3D, "Y"),
        new EditorShortcutBinding("map3d.thing-align-to-wall", EditorCommandScope.Map3D, "A", Accelerator: true, Shift: true),
        new EditorShortcutBinding("map3d.scale-up", EditorCommandScope.Map3D, "NumPad9"),
        new EditorShortcutBinding("map3d.scale-down", EditorCommandScope.Map3D, "NumPad7"),
        new EditorShortcutBinding("map3d.scale-up-x", EditorCommandScope.Map3D, "NumPad6"),
        new EditorShortcutBinding("map3d.scale-down-x", EditorCommandScope.Map3D, "NumPad4"),
        new EditorShortcutBinding("map3d.scale-up-y", EditorCommandScope.Map3D, "NumPad8"),
        new EditorShortcutBinding("map3d.scale-down-y", EditorCommandScope.Map3D, "NumPad5"),
        new EditorShortcutBinding("map3d.rotate-clockwise", EditorCommandScope.Map3D, EditorPointerInput.ScrollUp, Accelerator: true, Shift: true),
        new EditorShortcutBinding("map3d.rotate-counterclockwise", EditorCommandScope.Map3D, EditorPointerInput.ScrollDown, Accelerator: true, Shift: true),
        new EditorShortcutBinding("map3d.pitch-clockwise", EditorCommandScope.Map3D, EditorPointerInput.ScrollUp, Accelerator: true, Alt: true),
        new EditorShortcutBinding("map3d.pitch-counterclockwise", EditorCommandScope.Map3D, EditorPointerInput.ScrollDown, Accelerator: true, Alt: true),
        new EditorShortcutBinding("map3d.roll-clockwise", EditorCommandScope.Map3D, EditorPointerInput.ScrollUp, Alt: true),
        new EditorShortcutBinding("map3d.roll-counterclockwise", EditorCommandScope.Map3D, EditorPointerInput.ScrollDown, Alt: true),
        new EditorShortcutBinding("map3d.nudge-offset-left", EditorCommandScope.Map3D, "Left", Shift: true),
        new EditorShortcutBinding("map3d.nudge-offset-right", EditorCommandScope.Map3D, "Right", Shift: true),
        new EditorShortcutBinding("map3d.nudge-offset-up", EditorCommandScope.Map3D, "Up", Shift: true),
        new EditorShortcutBinding("map3d.nudge-offset-down", EditorCommandScope.Map3D, "Down", Shift: true),
    };

    public static IReadOnlyList<EditorCommandDescriptor> ByScope(EditorCommandScope scope)
        => All.Where(command => command.Scope == scope).ToArray();

    public static EditorCommandDescriptor? Find(string commandId)
        => All.FirstOrDefault(command => string.Equals(command.Id, commandId, StringComparison.Ordinal));

    public static string GestureText(string commandId, IReadOnlyList<EditorShortcutBinding> bindings)
    {
        var gestures = bindings
            .Where(binding => string.Equals(binding.CommandId, commandId, StringComparison.Ordinal))
            .Select(GestureText)
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return gestures.Length == 0 ? "-" : string.Join(" / ", gestures);
    }

    public static string CommandHint(string commandId, IReadOnlyList<EditorShortcutBinding> bindings)
    {
        var command = Find(commandId);
        if (command is null) return commandId;
        string gesture = GestureText(commandId, bindings);
        return gesture == "-" ? command.Title : $"{gesture} {command.Title}";
    }

    public static string CommandHints(IReadOnlyList<EditorShortcutBinding> bindings, params string[] commandIds)
        => string.Join("; ", commandIds.Select(commandId => CommandHint(commandId, bindings)));

    public static string CommandToolTip(string label, string commandId, IReadOnlyList<EditorShortcutBinding> bindings)
    {
        string gesture = GestureText(commandId, bindings);
        return gesture == "-" ? label : $"{label} ({gesture})";
    }

    public static string GestureText(EditorShortcutBinding binding)
    {
        if (string.IsNullOrWhiteSpace(binding.Key)) return "";
        var parts = new List<string>();
        if (binding.Accelerator) parts.Add("Ctrl/Cmd");
        if (binding.Alt) parts.Add("Alt");
        if (binding.Shift) parts.Add("Shift");
        parts.Add(DisplayKey(binding.Key));
        return string.Join("+", parts);
    }

    public static string OverrideText(IEnumerable<EditorShortcutBinding> overrides)
        => string.Join("; ", overrides.Select(binding => $"{binding.CommandId}={GestureText(binding)}"));

    public static List<EditorShortcutBinding> ParseOverrideText(string? text)
    {
        var result = new List<EditorShortcutBinding>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var byId = All.ToDictionary(command => command.Id, StringComparer.Ordinal);
        foreach (string rawEntry in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int equals = rawEntry.IndexOf('=');
            if (equals <= 0 || equals == rawEntry.Length - 1) continue;

            string commandId = rawEntry[..equals].Trim();
            string gesture = rawEntry[(equals + 1)..].Trim();
            if (!byId.TryGetValue(commandId, out var command)) continue;
            if (!TryParseGesture(commandId, command.Scope, gesture, out var binding)) continue;
            result.Add(binding);
        }

        return result;
    }

    public static bool TryParseGesture(string commandId, EditorCommandScope scope, string gesture, out EditorShortcutBinding binding)
    {
        binding = new EditorShortcutBinding(commandId, scope, "");
        if (string.IsNullOrWhiteSpace(gesture)) return false;

        bool accelerator = false, shift = false, alt = false;
        string key = "";
        foreach (string rawPart in SplitGestureParts(gesture))
        {
            string part = rawPart.Trim();
            if (IsAcceleratorAlias(part))
                accelerator = true;
            else if (IsShiftAlias(part))
                shift = true;
            else if (IsAltAlias(part))
                alt = true;
            else
                key = ParseDisplayKey(part);
        }

        if (key.Length == 0) return false;
        binding = new EditorShortcutBinding(commandId, scope, key, accelerator, shift, alt);
        return true;
    }

    private static bool IsAcceleratorAlias(string part)
        => part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
           || part.Equals("Control", StringComparison.OrdinalIgnoreCase)
           || part.Equals("ControlKey", StringComparison.OrdinalIgnoreCase)
           || part.Equals("LControlKey", StringComparison.OrdinalIgnoreCase)
           || part.Equals("RControlKey", StringComparison.OrdinalIgnoreCase)
           || part.Equals("Cmd", StringComparison.OrdinalIgnoreCase)
           || part.Equals("Command", StringComparison.OrdinalIgnoreCase)
           || part.Equals("Ctrl/Cmd", StringComparison.OrdinalIgnoreCase);

    private static bool IsShiftAlias(string part)
        => part.Equals("Shift", StringComparison.OrdinalIgnoreCase)
           || part.Equals("ShiftKey", StringComparison.OrdinalIgnoreCase)
           || part.Equals("LShiftKey", StringComparison.OrdinalIgnoreCase)
           || part.Equals("RShiftKey", StringComparison.OrdinalIgnoreCase);

    private static bool IsAltAlias(string part)
        => part.Equals("Alt", StringComparison.OrdinalIgnoreCase)
           || part.Equals("Option", StringComparison.OrdinalIgnoreCase)
           || part.Equals("Menu", StringComparison.OrdinalIgnoreCase)
           || part.Equals("LMenu", StringComparison.OrdinalIgnoreCase)
           || part.Equals("RMenu", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SplitGestureParts(string gesture)
    {
        string text = gesture.Trim();
        if (text.Equals("+", StringComparison.OrdinalIgnoreCase))
        {
            yield return "+";
            yield break;
        }

        const string numpadPlus = "NumPad+";
        if (text.EndsWith(numpadPlus, StringComparison.OrdinalIgnoreCase))
            text = text[..^numpadPlus.Length] + "Add";
        else if (text.EndsWith("+", StringComparison.Ordinal))
            text = text[..^1] + "+OemPlus";

        foreach (string part in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return part;
    }

    private static string DisplayKey(string key) => key switch
    {
        "OemOpenBrackets" => "[",
        "OemCloseBrackets" => "]",
        "OemTilde" => "~",
        "OemSemicolon" => ";",
        "OemQuotes" => "'",
        "OemComma" => ",",
        "OemPeriod" => ".",
        "OemQuestion" => "?",
        "OemBackslash" => "\\",
        "OemPlus" => "+",
        "Add" => "NumPad+",
        "OemMinus" => "-",
        "Subtract" => "NumPad-",
        "Decimal" => "NumPad.",
        "Multiply" => "NumPad*",
        "Divide" => "NumPad/",
        "Escape" => "Esc",
        "Back" => "Backspace",
        "CapsLock" => "CapsLock",
        "D0" => "0",
        "D1" => "1",
        "D2" => "2",
        "D3" => "3",
        "D4" => "4",
        "D5" => "5",
        "D6" => "6",
        "D7" => "7",
        "D8" => "8",
        "D9" => "9",
        EditorPointerInput.LeftButton => EditorPointerInput.LeftButton,
        EditorPointerInput.MiddleButton => EditorPointerInput.MiddleButton,
        EditorPointerInput.RightButton => EditorPointerInput.RightButton,
        EditorPointerInput.ExtendedButton1 => EditorPointerInput.ExtendedButton1,
        EditorPointerInput.ExtendedButton2 => EditorPointerInput.ExtendedButton2,
        _ => key,
    };

    private static string ParseDisplayKey(string key)
    {
        if (key.Equals("Esc", StringComparison.OrdinalIgnoreCase)) return "Escape";
        if (key.Equals("Return", StringComparison.OrdinalIgnoreCase)) return "Enter";
        if (key.Equals("Del", StringComparison.OrdinalIgnoreCase)) return "Delete";
        if (key.Equals("Backspace", StringComparison.OrdinalIgnoreCase)) return "Back";
        if (key.Equals("Prior", StringComparison.OrdinalIgnoreCase)) return "PageUp";
        if (key.Equals("Next", StringComparison.OrdinalIgnoreCase)) return "PageDown";
        if (key.Equals("Capital", StringComparison.OrdinalIgnoreCase)) return "CapsLock";
        if (key.Equals("CapsLock", StringComparison.OrdinalIgnoreCase)) return "CapsLock";
        if (key.Equals("Spacebar", StringComparison.OrdinalIgnoreCase)) return "Space";
        if (key.Equals("SpaceKey", StringComparison.OrdinalIgnoreCase)) return "Space";
        if (key.Equals("NumPad+", StringComparison.OrdinalIgnoreCase)) return "Add";
        if (key.Equals("NumPad-", StringComparison.OrdinalIgnoreCase)) return "Subtract";
        if (key.Equals("NumPad.", StringComparison.OrdinalIgnoreCase)) return "Decimal";
        if (key.Equals("NumPad*", StringComparison.OrdinalIgnoreCase)) return "Multiply";
        if (key.Equals("NumPad/", StringComparison.OrdinalIgnoreCase)) return "Divide";
        if (key.Length == 4
            && key.StartsWith("Num", StringComparison.OrdinalIgnoreCase)
            && char.IsDigit(key[3]))
            return "NumPad" + key[3];
        if (key.Equals("LButton", StringComparison.OrdinalIgnoreCase)) return EditorPointerInput.LeftButton;
        if (key.Equals("MButton", StringComparison.OrdinalIgnoreCase)) return EditorPointerInput.MiddleButton;
        if (key.Equals("RButton", StringComparison.OrdinalIgnoreCase)) return EditorPointerInput.RightButton;
        if (key.Equals("XButton1", StringComparison.OrdinalIgnoreCase)) return EditorPointerInput.ExtendedButton1;
        if (key.Equals("XButton2", StringComparison.OrdinalIgnoreCase)) return EditorPointerInput.ExtendedButton2;

        return key switch
        {
            "[" => "OemOpenBrackets",
            "{" => "OemOpenBrackets",
            "]" => "OemCloseBrackets",
            "}" => "OemCloseBrackets",
            "Oemtilde" => "OemTilde",
            "Oem3" => "OemTilde",
            "~" => "OemTilde",
            "Oem1" => "OemSemicolon",
            ";" => "OemSemicolon",
            ":" => "OemSemicolon",
            "Oem7" => "OemQuotes",
            "'" => "OemQuotes",
            "\"" => "OemQuotes",
            "Oemcomma" => "OemComma",
            "," => "OemComma",
            "<" => "OemComma",
            "Oem2" => "OemQuestion",
            "." => "OemPeriod",
            ">" => "OemPeriod",
            "/" => "OemQuestion",
            "?" => "OemQuestion",
            "Oem4" => "OemOpenBrackets",
            "Oem5" => "OemBackslash",
            "OemPipe" => "OemBackslash",
            "Oem6" => "OemCloseBrackets",
            "\\" => "OemBackslash",
            "|" => "OemBackslash",
            "Oemplus" => "OemPlus",
            "+" => "OemPlus",
            "OemMinus" => "OemMinus",
            "-" => "OemMinus",
            "_" => "OemMinus",
            "0" => "D0",
            ")" => "D0",
            "1" => "D1",
            "!" => "D1",
            "2" => "D2",
            "@" => "D2",
            "3" => "D3",
            "#" => "D3",
            "4" => "D4",
            "$" => "D4",
            "5" => "D5",
            "%" => "D5",
            "6" => "D6",
            "^" => "D6",
            "7" => "D7",
            "&" => "D7",
            "8" => "D8",
            "*" => "D8",
            "9" => "D9",
            "(" => "D9",
            _ => key,
        };
    }

    public static IReadOnlyList<EditorShortcutBinding> EffectiveShortcuts(IEnumerable<EditorShortcutBinding>? overrides)
    {
        if (overrides is null) return DefaultShortcuts;

        var known = All.Select(command => command.Id).ToHashSet(StringComparer.Ordinal);
        var validOverrides = overrides
            .Where(binding => known.Contains(binding.CommandId) && !string.IsNullOrWhiteSpace(binding.Key))
            .ToArray();
        if (validOverrides.Length == 0) return DefaultShortcuts;

        var replaced = validOverrides.Select(binding => binding.CommandId).ToHashSet(StringComparer.Ordinal);
        return DefaultShortcuts
            .Where(binding => !replaced.Contains(binding.CommandId))
            .Concat(validOverrides)
            .ToArray();
    }

    public static string? ResolveShortcut(EditorCommandScope scope, string key, bool accelerator = false, bool shift = false, bool alt = false)
        => ResolveShortcut(DefaultShortcuts, scope, key, accelerator, shift, alt);

    public static string? ResolveShortcut(
        IReadOnlyList<EditorShortcutBinding> bindings,
        EditorCommandScope scope,
        string key,
        bool accelerator = false,
        bool shift = false,
        bool alt = false)
    {
        for (int i = bindings.Count - 1; i >= 0; i--)
        {
            var shortcut = bindings[i];
            var command = Find(shortcut.CommandId);
            if (shortcut.Scope == scope
                && InputKindAllowed(command, key)
                && ModifierMatches(shortcut.Accelerator, accelerator, command?.DisregardAccelerator ?? false)
                && ModifierMatches(shortcut.Shift, shift, command?.DisregardShift ?? false)
                && ModifierMatches(shortcut.Alt, alt, command?.DisregardAlt ?? false)
                && string.Equals(NormalizeKey(shortcut.Key), NormalizeKey(key), StringComparison.OrdinalIgnoreCase))
            {
                return shortcut.CommandId;
            }
        }

        return null;
    }

    private static bool InputKindAllowed(EditorCommandDescriptor? command, string key)
    {
        if (command is null) return false;
        string normalized = NormalizeKey(key);
        if (EditorPointerInput.IsScrollKey(normalized)) return command.AllowScroll;
        if (EditorPointerInput.IsButtonKey(normalized)) return command.AllowMouse;
        return command.AllowKeys;
    }

    private static bool ModifierMatches(bool expected, bool actual, bool disregard)
        => disregard || expected == actual;

    public static bool IsRepeatable(string commandId) => Find(commandId)?.Repeat ?? false;

    public static string ShortcutPressKey(EditorCommandScope scope, string key, bool accelerator = false, bool shift = false, bool alt = false)
        => $"{scope}:{NormalizeKey(key)}:{accelerator}:{shift}:{alt}";

    public static string ShortcutReleasePrefix(EditorCommandScope scope, string key)
        => $"{scope}:{NormalizeKey(key)}:";

    private static string NormalizeKey(string key) => ParseDisplayKey(key);
}
