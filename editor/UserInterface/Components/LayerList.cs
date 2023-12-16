using System;
using System.Linq;
using System.Numerics;
using BrewLib.UserInterface;
using BrewLib.Util;
using StorybrewCommon.Storyboarding;
using StorybrewEditor.Storyboarding;

namespace StorybrewEditor.UserInterface.Components;

public class LayerList : Widget
{
    readonly LinearLayout layout, layersLayout;
    LayerManager layerManager;

    public override Vector2 MinSize => layout.MinSize;
    public override Vector2 MaxSize => layout.MaxSize;
    public override Vector2 PreferredSize => layout.PreferredSize;

    public event Action<EditorStoryboardLayer> OnLayerPreselect, OnLayerSelected;

    public LayerList(WidgetManager manager, LayerManager layerManager) : base(manager)
    {
        this.layerManager = layerManager;

        Add(layout = new(manager)
        {
            StyleName = "panel",
            Padding = new(16),
            FitChildren = true,
            Fill = true,
            Children = new Widget[]
            {
                new Label(manager)
                {
                    Text = "Layers",
                    CanGrow = false
                },
                new ScrollArea(manager, layersLayout = new(manager)
                {
                    FitChildren = true
                })
            }
        });

        layerManager.OnLayersChanged += layerManager_OnLayersChanged;
        refreshLayers();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) layerManager.OnLayersChanged -= layerManager_OnLayersChanged;
        layerManager = null;
        base.Dispose(disposing);
    }
    protected override void Layout()
    {
        base.Layout();
        layout.Size = Size;
    }

    void layerManager_OnLayersChanged(object sender, EventArgs e) => refreshLayers();
    void refreshLayers()
    {
        layersLayout.ClearWidgets();
        foreach (var osbLayer in Project.OsbLayers)
        {
            Label osbLayerLabel;
            layersLayout.Add(osbLayerLabel = new(Manager)
            {
                StyleName = "listHeader",
                Text = osbLayer.ToString()
            });

            var ol = osbLayer;
            osbLayerLabel.HandleDrop = data =>
            {
                if (data is EditorStoryboardLayer droppedLayer)
                {
                    var dndLayer = layerManager.Layers.FirstOrDefault(l => l.Guid == droppedLayer.Guid);
                    if (dndLayer is not null) layerManager.MoveToOsbLayer(dndLayer, ol);
                    return true;
                }
                return false;
            };

            buildLayers(osbLayer, true);
            buildLayers(osbLayer, false);
        }
    }
    void buildLayers(OsbLayer osbLayer, bool diffSpecific)
    {
        var layers = layerManager.FindLayers(l => l.OsbLayer == osbLayer && l.DiffSpecific == diffSpecific);

        var index = 0;
        foreach (var layer in layers)
        {
            var effect = layer.Effect;

            LinearLayout layerRoot;
            Label nameLabel, detailsLabel;
            Button diffSpecificButton, showHideButton;

            layersLayout.Add(layerRoot = new(Manager)
            {
                AnchorFrom = BoxAlignment.Centre,
                AnchorTo = BoxAlignment.Centre,
                Horizontal = true,
                FitChildren = true,
                Fill = true,
                Children = new Widget[]
                {
                    new Label(Manager)
                    {
                        StyleName = "icon",
                        Icon = IconFont.Sort,
                        Tooltip = "Drag to reorder",
                        AnchorFrom = BoxAlignment.Centre,
                        AnchorTo = BoxAlignment.Centre,
                        CanGrow = false
                    },
                    new LinearLayout(Manager)
                    {
                        StyleName = "condensed",
                        Children = new Widget[]
                        {
                            nameLabel = new(Manager)
                            {
                                StyleName = "listItem",
                                Text = layer.Identifier,
                                AnchorFrom = BoxAlignment.Left,
                                AnchorTo = BoxAlignment.Left
                            },
                            detailsLabel = new(Manager)
                            {
                                StyleName = "listItemSecondary",
                                Text = getLayerDetails(layer, effect),
                                AnchorFrom = BoxAlignment.Left,
                                AnchorTo = BoxAlignment.Left
                            }
                        }
                    },
                    diffSpecificButton = new(Manager)
                    {
                        StyleName = "icon",
                        Icon = layer.DiffSpecific ? IconFont.InsertDriveFile : IconFont.FileCopy,
                        Tooltip = layer.DiffSpecific ? "Difficulty specific\n(exports to .osu)" : "Entire mapset\n(exports to .osb)",
                        AnchorFrom = BoxAlignment.Centre,
                        AnchorTo = BoxAlignment.Centre,
                        CanGrow = false
                    },
                    showHideButton = new(Manager)
                    {
                        StyleName = "icon",
                        Icon = layer.Visible ? IconFont.Visibility : IconFont.VisibilityOff,
                        Tooltip = "Show/Hide",
                        AnchorFrom = BoxAlignment.Centre,
                        AnchorTo = BoxAlignment.Centre,
                        Checkable = true,
                        Checked = layer.Visible,
                        CanGrow = false
                    }
                }
            });

            var la = layer;

            layerRoot.GetDragData = () => la;
            layerRoot.HandleDrop = data =>
            {
                if (data is EditorStoryboardLayer droppedLayer)
                {
                    if (droppedLayer.Guid != la.Guid)
                    {
                        var dndLayer = layerManager.Layers.FirstOrDefault(l => l.Guid == droppedLayer.Guid);
                        if (dndLayer is not null) layerManager.MoveToLayer(dndLayer, la);
                    }
                    return true;
                }
                return false;
            };

            ChangedHandler changedHandler;
            EventHandler effectChangedHandler;

            layer.OnChanged += changedHandler = (sender, e) =>
            {
                nameLabel.Text = la.Identifier;
                diffSpecificButton.Icon = la.DiffSpecific ? IconFont.InsertDriveFile : IconFont.FileCopy;
                diffSpecificButton.Tooltip = la.DiffSpecific ? "Difficulty specific\n(exports to .osu)" : "Entire mapset\n(exports to .osb)";
                showHideButton.Icon = la.Visible ? IconFont.Visibility : IconFont.VisibilityOff;
                showHideButton.Checked = la.Visible;
            };
            effect.OnChanged += effectChangedHandler = (sender, e) => detailsLabel.Text = getLayerDetails(la, effect);
            layerRoot.OnHovered += (evt, e) =>
            {
                la.Highlight = e.Hovered;
                OnLayerPreselect?.Invoke(e.Hovered ? la : null);
            };
            var handledClick = false;
            layerRoot.OnClickDown += (evt, e) =>
            {
                handledClick = true;
                return true;
            };
            layerRoot.OnClickUp += (evt, e) =>
            {
                if (handledClick && (evt.RelatedTarget == layerRoot || evt.RelatedTarget.HasAncestor(layerRoot))) OnLayerSelected?.Invoke(la);
                handledClick = false;
            };
            layerRoot.OnDisposed += (sender, e) =>
            {
                la.Highlight = false;
                la.OnChanged -= changedHandler;
                effect.OnChanged -= effectChangedHandler;
            };

            diffSpecificButton.OnClick += (sender, e) => la.DiffSpecific = !la.DiffSpecific;
            showHideButton.OnValueChanged += (sender, e) => la.Visible = showHideButton.Checked;
            ++index;
        }
    }

    static string getLayerDetails(EditorStoryboardLayer layer, Effect effect) => layer.EstimatedSize > 30720 ?
        $"using {effect.BaseName} ({StringHelper.ToByteSize(layer.EstimatedSize)})" : $"using {effect.BaseName}";
}