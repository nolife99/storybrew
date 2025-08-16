namespace StorybrewEditor.UserInterface.Components;

using System;
using System.Numerics;
using BrewLib.UserInterface;
using BrewLib.Util;
using StorybrewCommon.Storyboarding;
using StorybrewEditor.Storyboarding;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public class LayerList : Widget
{
    readonly LayerManager layerManager;
    readonly LinearLayout layout, layersLayout;

    public LayerList(WidgetManager manager, LayerManager layerManager) : base(manager)
    {
        this.layerManager = layerManager;

        Add(layout = new(manager)
        {
            StyleName = "panel",
            Padding = new(16),
            FitChildren = true,
            Fill = true,
            Children =
            [
                new Label(manager) { Text = "Layers", CanGrow = false },
                new ScrollArea(manager, layersLayout = new(manager) { FitChildren = true })
            ]
        });

        layerManager.OnLayersChanged += layerManager_OnLayersChanged;
        refreshLayers();
    }

    public override Vector2 MinSize => layout.MinSize;
    public override Vector2 MaxSize => layout.MaxSize;
    public override Vector2 PreferredSize => layout.PreferredSize;

    public event Action<EditorStoryboardLayer> OnLayerPreselect, OnLayerSelected;

    protected override void Dispose(bool disposing)
    {
        if (disposing) layerManager.OnLayersChanged -= layerManager_OnLayersChanged;

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
            using var text = StringHelper.Interpolate($"{osbLayer}");
            layersLayout.Add(new Label(Manager)
            {
                StyleName = "listHeader",
                Text = text.AsReadOnlySpan(),
                HandleDrop = data =>
                {
                    if (data is not EditorStoryboardLayer droppedLayer) return false;

                    foreach (var dndLayer in layerManager.Layers)
                        if (dndLayer.Identifier == droppedLayer.Identifier)
                        {
                            layerManager.MoveToOsbLayer(dndLayer, osbLayer);
                            break;
                        }

                    return true;
                }
            });

            buildLayers(osbLayer, true);
            buildLayers(osbLayer, false);
        }
    }

    void buildLayers(OsbLayer osbLayer, bool diffSpecific)
    {
        foreach (var layer in layerManager.Layers)
        {
            if (layer.OsbLayer != osbLayer || layer.DiffSpecific != diffSpecific) continue;

            var effect = layer.Effect;

            LinearLayout layerRoot;
            Label nameLabel, detailsLabel;
            Button diffSpecificButton, showHideButton;

            using var text = getLayerDetails(layer, effect);

            layersLayout.Add(layerRoot = new(Manager)
            {
                AnchorFrom = BoxAlignment.Centre,
                AnchorTo = BoxAlignment.Centre,
                Horizontal = true,
                FitChildren = true,
                Fill = true,
                Children =
                [
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
                        Children =
                        [
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
                                Text = text.AsReadOnlySpan(),
                                AnchorFrom = BoxAlignment.Left,
                                AnchorTo = BoxAlignment.Left
                            }
                        ]
                    },
                    diffSpecificButton = new(Manager)
                    {
                        StyleName = "icon",
                        Icon = layer.DiffSpecific ? IconFont.InsertDriveFile : IconFont.FileCopy,
                        Tooltip =
                            layer.DiffSpecific ?
                                "Difficulty specific\n(exports to .osu)" :
                                "Entire mapset\n(exports to .osb)",
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
                ],
                GetDragData = () => layer,
                HandleDrop = data =>
                {
                    if (data is not EditorStoryboardLayer droppedLayer) return false;

                    if (droppedLayer.Identifier == layer.Identifier) return true;

                    foreach (var dndLayer in layerManager.Layers)
                        if (dndLayer.Identifier == droppedLayer.Identifier)
                        {
                            layerManager.MoveToOsbLayer(dndLayer, osbLayer);
                            break;
                        }

                    return true;
                }
            });

            Action<object, ChangedEventArgs> changedHandler;
            Action<Effect> effectChangedHandler;

            layer.OnChanged += changedHandler = (_, _) =>
            {
                nameLabel.Text = layer.Identifier;
                diffSpecificButton.Icon = layer.DiffSpecific ? IconFont.InsertDriveFile : IconFont.FileCopy;

                diffSpecificButton.Tooltip = layer.DiffSpecific ?
                    "Difficulty specific\n(exports to .osu)" :
                    "Entire mapset\n(exports to .osb)";

                showHideButton.Icon = layer.Visible ? IconFont.Visibility : IconFont.VisibilityOff;

                showHideButton.Checked = layer.Visible;
            };

            effect.Changed += effectChangedHandler = _ =>
            {
                using var text = getLayerDetails(layer, layer.Effect);
                detailsLabel.Text = text.AsReadOnlySpan();
            };

            layerRoot.OnHovered += (_, e) =>
            {
                layer.Highlight = e.Hovered;
                OnLayerPreselect?.Invoke(e.Hovered ? layer : null);
            };

            var handledClick = false;
            layerRoot.OnClickDown += (_, _) =>
            {
                handledClick = true;
                return true;
            };

            layerRoot.OnClickUp += (evt, _) =>
            {
                if (handledClick && (evt.RelatedTarget == layerRoot || evt.RelatedTarget.HasAncestor(layerRoot)))
                    OnLayerSelected?.Invoke(layer);

                handledClick = false;
            };

            layerRoot.OnDisposed += (_, _) =>
            {
                layer.Highlight = false;
                layer.OnChanged -= changedHandler;
                layer.Effect.Changed -= effectChangedHandler;
            };

            diffSpecificButton.OnClick += (_, _) => layer.DiffSpecific = !layer.DiffSpecific;

            showHideButton.OnValueChanged += (_, _) => layer.Visible = showHideButton.Checked;
        }
    }

    static TempList<char> getLayerDetails(EditorStoryboardLayer layer, Effect effect)
    {
        var str = TempList.Create("using ");
        if (layer.EstimatedSize > 30720)
            str.Append($"{effect.BaseName} ({StringHelper.ToByteSize(layer.EstimatedSize)})");
        else str.AddRange(effect.BaseName);

        return str;
    }
}