#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Scripting;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public enum IngameInfoPanel { AutoSelect, Map, Objectives, Debug, Chat }

	class GameInfoLogic : ChromeLogic
	{
		readonly World world;
		readonly Action<bool> hideMenu;
		readonly IObjectivesPanel iop;
		IngameInfoPanel activePanel;
		bool hasError;

		[ObjectCreator.UseCtor]
		public GameInfoLogic(Widget widget, World world, IngameInfoPanel initialPanel, Action<bool> hideMenu)
		{
			var panels = new Dictionary<IngameInfoPanel, (string Panel, string Label, Action<ButtonWidget, Widget> Setup)>()
			{
				{ IngameInfoPanel.Objectives, ("OBJECTIVES_PANEL", "Objectives", SetupObjectivesPanel) },
				{ IngameInfoPanel.Map, ("MAP_PANEL", "Briefing", SetupMapPanel) },
				{ IngameInfoPanel.Debug, ("DEBUG_PANEL", "Debug", SetupDebugPanel) },
				{ IngameInfoPanel.Chat, ("CHAT_PANEL", "Chat", SetupChatPanel) }
			};

			this.world = world;
			this.hideMenu = hideMenu;
			activePanel = initialPanel;

			var visiblePanels = new List<IngameInfoPanel>();

			// Objectives/Stats tab
			var scriptContext = world.WorldActor.TraitOrDefault<LuaScript>();
			hasError = scriptContext != null && scriptContext.FatalErrorOccurred;
			iop = world.WorldActor.TraitsImplementing<IObjectivesPanel>().FirstOrDefault();

			if (hasError || (iop != null && iop.PanelName != null))
				visiblePanels.Add(IngameInfoPanel.Objectives);

			// Briefing tab
			var missionData = world.WorldActor.Info.TraitInfoOrDefault<MissionDataInfo>();
			if (missionData != null && !string.IsNullOrEmpty(missionData.Briefing))
				visiblePanels.Add(IngameInfoPanel.Map);

			// Debug/Cheats tab
			// Can't use DeveloperMode.Enabled because there is a hardcoded hack to *always*
			// enable developer mode for singleplayer games, but we only want to show the button
			// if it has been explicitly enabled
			var def = world.Map.Rules.Actors[SystemActors.Player].TraitInfo<DeveloperModeInfo>().CheckboxEnabled;
			var developerEnabled = world.LobbyInfo.GlobalSettings.OptionOrDefault("cheats", def);
			if (world.LocalPlayer != null && developerEnabled)
				visiblePanels.Add(IngameInfoPanel.Debug);

			if (world.LobbyInfo.NonBotClients.Count() > 1)
				visiblePanels.Add(IngameInfoPanel.Chat);

			var numTabs = visiblePanels.Count;
			var tabContainer = !hasError ? widget.GetOrNull($"TAB_CONTAINER_{numTabs}") : null;
			if (tabContainer != null)
				tabContainer.IsVisible = () => true;

			for (var i = 0; i < numTabs; i++)
			{
				var type = visiblePanels[i];
				var info = panels[type];
				var tabButton = tabContainer?.Get<ButtonWidget>($"BUTTON{i + 1}");

				if (tabButton != null)
				{
					tabButton.Text = info.Label;
					tabButton.OnClick = () => activePanel = type;
					tabButton.IsHighlighted = () => activePanel == type;
				}

				var panelContainer = widget.Get<ContainerWidget>(info.Panel);
				panelContainer.IsVisible = () => activePanel == type;
				info.Setup(tabButton, panelContainer);

				if (activePanel == IngameInfoPanel.AutoSelect)
					activePanel = type;
			}

			// Handle empty space when tabs aren't displayed
			var titleText = widget.Get<LabelWidget>("TITLE");
			var titleTextNoTabs = widget.GetOrNull<LabelWidget>("TITLE_NO_TABS");

			var mapTitle = world.Map.Title;
			var firstCategory = world.Map.Categories.FirstOrDefault();
			if (firstCategory != null)
				mapTitle = firstCategory + ": " + mapTitle;

			titleText.IsVisible = () => numTabs > 1 || (numTabs == 1 && titleTextNoTabs == null);
			titleText.GetText = () => mapTitle;
			if (titleTextNoTabs != null)
			{
				titleTextNoTabs.IsVisible = () => numTabs == 1;
				titleTextNoTabs.GetText = () => mapTitle;
			}

			var bg = widget.Get<BackgroundWidget>("BACKGROUND");
			var bgNoTabs = widget.GetOrNull<BackgroundWidget>("BACKGROUND_NO_TABS");

			bg.IsVisible = () => numTabs > 1 || (numTabs == 1 && bgNoTabs == null);
			if (bgNoTabs != null)
				bgNoTabs.IsVisible = () => numTabs == 1;
		}

		void SetupObjectivesPanel(ButtonWidget objectivesTabButton, Widget objectivesPanelContainer)
		{
			var panel = hasError ? "SCRIPT_ERROR_PANEL" : iop.PanelName;
			Game.LoadWidget(world, panel, objectivesPanelContainer, new WidgetArgs()
			{
				{ "hideMenu", hideMenu }
			});
		}

		void SetupMapPanel(ButtonWidget mapTabButton, Widget mapPanelContainer)
		{
			Game.LoadWidget(world, "MAP_PANEL", mapPanelContainer, new WidgetArgs());
		}

		void SetupDebugPanel(ButtonWidget debugTabButton, Widget debugPanelContainer)
		{
			if (debugTabButton != null)
				debugTabButton.IsDisabled = () => world.IsGameOver;

			Game.LoadWidget(world, "DEBUG_PANEL", debugPanelContainer, new WidgetArgs());

			if (activePanel == IngameInfoPanel.AutoSelect)
				activePanel = IngameInfoPanel.Debug;
		}

		void SetupChatPanel(ButtonWidget chatTabButton, Widget chatPanelContainer)
		{
			if (chatTabButton != null)
			{
				var lastOnClick = chatTabButton.OnClick;
				chatTabButton.OnClick = () =>
				{
					lastOnClick();
					chatPanelContainer.Get<TextFieldWidget>("CHAT_TEXTFIELD").TakeKeyboardFocus();
				};
			}

			Game.LoadWidget(world, "CHAT_CONTAINER", chatPanelContainer, new WidgetArgs() { { "isMenuChat", true } });
		}
	}
}
