using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.WarThunder;
using Microsoft.Win32;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>Projects native commands and occurrence records independently of persistence.</summary>
    internal sealed class WarThunderAssignmentBrowser(
        WarThunderWorkspaceContext context,
        Func<WarThunderAssignmentEditing> editing,
        WarThunderAssignmentsView _assignments)
    {
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        internal void RefreshCommands()
        {
            var browser = _assignments.Browser;
            var scope = (context.Session?.Snapshot.Id.ToString("N") ?? "") + ":" + context.SelectedDevice + ":" + browser.CommandCategoryPicker.SelectedItem + ":" + browser.CommandTypePicker.SelectedIndex;
            if (context.Selection.Scope != scope) context.SelectedPhysicalInput = null;
            context.Selection.SelectScope(scope);
            var selected = (browser.CommandsList.SelectedItem as SimCommand)?.Id;
            var catalog = context.Session?.Catalog.Commands ?? [];
            var owned = context.Rows.Where(row => context.SelectedDevice is null || row.DeviceIds.Contains(context.SelectedDevice)).ToArray();
            _assignments.AssignmentCount.Text = owned.Count(row => row.DeviceIds.Count > 0) + " captured joystick assignments · " + context.Rows.Count + " native records";
            var commands = WarThunderWorkspaceProjection.Filter(catalog, context.Rows, context.SelectedDevice, browser.CommandSearch.Text,
                browser.CommandCategoryPicker.SelectedIndex > 0 ? browser.CommandCategoryPicker.SelectedItem as string : null,
                (browser.CommandTypePicker.SelectedItem as GameCommandTypeOption)?.Type,
                (browser.AssignmentStatusPicker.SelectedItem as GameCommandStatusOption)?.Status ?? GameCommandAssignmentStatus.All);
            context.Refreshing = true;
            try
            {
                context.Selection.Refresh(context.SelectedDevice, commands, selected);
                browser.CommandsList.ItemsSource = commands;
                browser.CommandsList.SelectedItem = commands.FirstOrDefault(command => command.Id == selected)
                    ?? (commands.Count > 0 ? commands[0] : null);
                browser.CommandCount.Text = commands.Count + " captured commands";
                browser.CommandEmptyHint.Visibility = commands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                if ((browser.CommandsList.SelectedItem as SimCommand)?.Id != selected) context.SelectedPhysicalInput = null;
            }
            finally { context.Refreshing = false; }
            RefreshRecords();
        }

        internal void RefreshRecords()
        {
            var selected = (_assignments.Records.SelectedItem as WarThunderWorkspaceRow)?.RecordId;
            var command = _assignments.Browser.CommandsList.SelectedItem as SimCommand;
            var rows = context.Rows.Where(row => _assignments.Browser.CommandsList.Items.Count > 0 && (command is null || row.Command.Id == command.Id)
                && (command is not null || context.SelectedDevice is null || row.DeviceIds.Contains(context.SelectedDevice) || row.Binding.Empty)).ToArray();
            context.Refreshing = true; _assignments.Records.ItemsSource = rows;
            _assignments.Records.SelectedItem = rows.FirstOrDefault(row => row.RecordId == selected) ?? rows.FirstOrDefault();
            context.Refreshing = false; editing().RefreshEditor();
        }
        #endregion END private helpers
    }
}
