using Gaya.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Turian.Editor.Core;
using Turian.Engine.Core;

namespace Turian.Editor.DataAssetDriven;

/// <summary>Contributes DataAsset authoring and runtime diagnostics as a non-runtime Studio brick.</summary>
[Plugin("turian.ddd.studio", "DataAsset-Driven Design")]
[PluginDependsOn("gaya.turian")]
public sealed class DataAssetStudioPlugin : IPlugin
{
    const string panelId = "turian.ddd.studio.diagnostics";

    /// <inheritdoc />
    public void Configure(IPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RegisterTypeGenerator(context);
        context.Services.AddSingleton<IAssetCreationProvider, DataAssetCreation>();
        context.Services.AddSingleton<IAssetPresentationProvider, DataAssetPresentation>();
        context.Services.AddSingleton<DataAssetTimeline>();
        context.Services.AddSingleton<IDataAssetEventObserver>(static services =>
            services.GetRequiredService<DataAssetTimeline>());
        context.Panels.Register(new PanelDescriptor(panelId, "DataAsset Overview", PanelPlacement.Bottom,
            static services => new DataAssetPanel(services.GetRequiredService<PlayModeService>(),
                services.GetRequiredService<DataAssetTimeline>(), services.GetRequiredService<SettingsService>(),
                services.GetRequiredService<AssetDatabase>(), services.GetService<AssetCreationCatalog>()))
        { OpenByDefault = false });
        context.Commands.Register(new CommandDescriptor(panelId, "DataAsset Overview",
            static services => services.GetRequiredService<IShellHost>().ShowPanel(panelId)));
        context.Menus.Add(new MenuItemDescriptor(MenuIds.View, panelId, "8"));
    }

    static void RegisterTypeGenerator(IPluginContext context)
    {
        const string id = "turian.ddd.studio.generator";
        context.Panels.Register(new PanelDescriptor(id, "DDD Type Generator", PanelPlacement.Center,
            static services => new DataAssetTypeGeneratorPanel(services.GetRequiredService<SettingsService>(),
                services.GetRequiredService<ProjectSession>().RecompileScripts))
        { OpenByDefault = false });
        context.Commands.Register(new CommandDescriptor(id, "DDD Type Generator",
            static services => services.GetRequiredService<IShellHost>().ShowPanel(id)));
        context.Menus.Add(new MenuItemDescriptor(MenuIds.View, id, "8"));
    }

}
