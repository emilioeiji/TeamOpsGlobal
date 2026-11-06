using Dapper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using TeamOps.Services;
using TeamOps.UI.Forms.Models;
using TeamOps.UI.Services;

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormProductionMonitor : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly User _currentUser;
        private readonly Operator _currentOperator;
        private readonly ProductionAnalyticsService _analyticsService;
        private readonly GBareruCapacityForecastService _forecastService;
        private readonly ProductionFileImporter _fileImporter;
        private readonly AuthorizationService _authorization;
        private readonly SemaphoreSlim _databaseOperationGate = new(1, 1);
        private readonly Dictionary<string, string> _operatorDetailJsonCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _operatorDetailCacheLock = new();
        private volatile bool _isImportingProduction;

        public HTMLFormProductionMonitor(
            SqliteConnectionFactory factory,
            User currentUser,
            Operator currentOperator)
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Monitor de producao", "\u751f\u7523\u30e2\u30cb\u30bf\u30fc");

            _factory = factory;
            _currentUser = currentUser;
            _currentOperator = currentOperator;

            var machineRepository = new ProductionMachineRepository(factory);
            var eventRepository = new ProductionEventRepository(factory);

            _analyticsService = new ProductionAnalyticsService(factory);
            _forecastService = new GBareruCapacityForecastService(factory);
            _fileImporter = new ProductionFileImporter(factory, machineRepository, eventRepository);
            _authorization = new AuthorizationService(factory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewProductionMonitor.EnsureCoreWebView2Async(null);

            var core = webViewProductionMonitor.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "production-monitor"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.Navigate("https://app/index.html");
        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var json = JsonDocument.Parse(e.WebMessageAsJson);
                var root = json.RootElement;
                var action = ReadString(root, "action");

                switch (action)
                {
                    case "production_init":
                        _ = RunAsync(LoadInitialAsync());
                        break;

                    case "production_load_dashboard":
                        if (!_isImportingProduction)
                        {
                            _ = RunAsync(SendDashboardAsync(ReadFilter(root)));
                        }
                        break;

                    case "production_import":
                        _ = ImportAndRefreshAsync(ReadFilter(root));
                        break;

                    case "production_reimport":
                        _ = ImportAndRefreshAsync(ReadFilter(root), cleanExistingEvents: true);
                        break;

                    case "production_machine_detail":
                        if (!_isImportingProduction)
                        {
                            _ = RunAsync(SendMachineDetailAsync(ReadInt(root, "machineId", 0)));
                        }
                        break;

                    case "production_operator_detail":
                        if (!_isImportingProduction)
                        {
                            _ = RunAsync(SendOperatorDetailAsync(ReadFilter(root), ReadString(root, "operatorCodigoFJ")));
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                PostJson(new
                {
                    type = "error",
                    message = ex.Message
                });
            }
        }

        private async Task RunAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                PostJson(new
                {
                    type = "error",
                    message = ex.Message
                });
            }
        }

        private async Task LoadInitialAsync()
        {
            _authorization.Demand(_currentUser.Id, ModuleCodes.Production, PermissionActions.View);
            var access = _authorization.GetContext(_currentUser.Id);
            var authorizedSectorIds = access.AuthorizedSectorIds;
            var defaultDate = DateTime.Today;
            var defaultShiftId = _currentOperator.ShiftId;
            var defaultSectorId = Program.CurrentSession?.SelectedSectorId
                ?? (string.Equals(access.ScopeMode, "OrganizationTree", StringComparison.Ordinal)
                    ? 0
                    : authorizedSectorIds.FirstOrDefault());

            await _databaseOperationGate.WaitAsync();
            try
            {
                var initPayload = await Task.Run(() =>
                {
                    using var conn = _factory.CreateOpenConnection();

                    return new
                    {
                        type = "init",
                        data = new
                        {
                            locale = Program.CurrentLocale,
                            currentUser = _currentUser.Name,
                            currentOperatorNamePt = _currentOperator.NameRomanji,
                            currentOperatorNameJp = string.IsNullOrWhiteSpace(_currentOperator.NameNihongo)
                                ? _currentOperator.NameRomanji
                                : _currentOperator.NameNihongo,
                            defaults = new
                            {
                                dateIso = defaultDate.ToString("yyyy-MM-dd"),
                                shiftId = defaultShiftId,
                                sectorId = defaultSectorId
                            },
                            shifts = conn.Query(
                                @"
                                    SELECT
                                        Id AS id,
                                        COALESCE(NamePt, '') AS namePt,
                                        COALESCE(NULLIF(NameJp, ''), NamePt, '') AS nameJp
                                    FROM Shifts
                                    ORDER BY Id;"
                            ).ToList(),
                            sectors = conn.Query(
                                @"
                                    SELECT
                                        Id AS id,
                                        COALESCE(NamePt, '') AS namePt,
                                        COALESCE(NULLIF(NameJp, ''), NamePt, '') AS nameJp
                                    FROM Sectors
                                    ORDER BY Id;"
                            ).Where(item => authorizedSectorIds.Contains((int)item.id)).ToList(),
                            locals = conn.Query(
                                @"
                                    SELECT
                                        l.Id AS id,
                                        l.SectorId AS sectorId,
                                        COALESCE(l.NamePt, '') AS namePt,
                                        COALESCE(NULLIF(l.NameJp, ''), l.NamePt, '') AS nameJp
                                    FROM Locals l
                                    ORDER BY l.SectorId, l.Id;"
                            ).Where(item => item.sectorId != null && authorizedSectorIds.Contains((int)item.sectorId)).ToList(),
                            machines = conn.Query(
                                @"
                                    SELECT
                                        m.Id AS id,
                                        COALESCE(m.MachineCode, '') AS machineCode,
                                        COALESCE(m.LineCode, '') AS lineCode,
                                        COALESCE(m.NamePt, '') AS namePt,
                                        COALESCE(NULLIF(m.NameJp, ''), m.NamePt, '') AS nameJp,
                                        m.SectorId AS sectorId,
                                        m.LocalId AS localId
                                    FROM Machines m
                                    WHERE COALESCE(m.IsActive, 1) = 1
                                    ORDER BY COALESCE(m.SectorId, 0), COALESCE(m.LocalId, 0), COALESCE(m.LineCode, ''), COALESCE(m.MachineCode, ''), COALESCE(m.NamePt, ''), m.Id;"
                            ).Where(item => item.sectorId != null && authorizedSectorIds.Contains((int)item.sectorId)).ToList(),
                            statuses = LoadStatuses(conn)
                        }
                    };
                });

                PostJson(initPayload);
            }
            finally
            {
                _databaseOperationGate.Release();
            }

            await SendDashboardAsync(new ProductionDashboardFilter
            {
                Date = defaultDate,
                ShiftId = defaultShiftId,
                SectorId = defaultSectorId
            });
        }

        private async Task SendDashboardAsync(ProductionDashboardFilter filter)
        {
            DemandProductionScope(PermissionActions.View, filter.SectorId);
            ClearOperatorDetailCache();
            await _databaseOperationGate.WaitAsync();
            try
            {
                var buildWatch = Stopwatch.StartNew();
                var dashboard = await Task.Run(() =>
                {
                    var builtDashboard = _analyticsService.BuildDashboard(filter);
                    builtDashboard.GBareruCapacityForecast = _forecastService.BuildForecast(filter, builtDashboard);
                    return builtDashboard;
                });
                buildWatch.Stop();

                LogDashboardDiagnostics(dashboard);

                var htmlOrJsonWatch = Stopwatch.StartNew();
                var payload = new
                {
                    type = "dashboard",
                    data = new
                    {
                        locale = Program.CurrentLocale,
                        dateIso = filter.Date.ToString("yyyy-MM-dd"),
                        shiftId = filter.ShiftId,
                        sectorId = filter.SectorId,
                        localId = filter.LocalId,
                        machineId = filter.MachineId,
                        machineCode = filter.MachineCode,
                        periodStart = dashboard.Period.Start.ToString("yyyy-MM-dd HH:mm:ss"),
                        periodEnd = dashboard.Period.End.ToString("yyyy-MM-dd HH:mm:ss"),
                        productionPercent = dashboard.ProductionPercent,
                        machinesRunning = dashboard.MachinesRunning,
                        machinesStopped = dashboard.MachinesStopped,
                        machinesIgnored = dashboard.MachinesIgnored,
                        machinesTotal = dashboard.MachinesTotal,
                        averageOperatingProcessMinutes = dashboard.AverageOperatingProcessMinutes,
                        errorMinutes = dashboard.ErrorMinutes,
                        inactiveMinutes = dashboard.InactiveMinutes,
                        machines = dashboard.Machines.Select(machine => new
                        {
                            machineId = machine.MachineId,
                            machineCode = machine.MachineCode,
                            machine = machine.Machine,
                            lineCode = machine.LineCode,
                            machineNamePt = machine.MachineNamePt,
                            machineNameJp = machine.MachineNameJp,
                            sectorId = machine.SectorId,
                            sectorNamePt = machine.SectorNamePt,
                            sectorNameJp = machine.SectorNameJp,
                            localId = machine.LocalId,
                            area = machine.Area,
                            localNamePt = machine.LocalNamePt,
                            localNameJp = machine.LocalNameJp,
                            statusCode = machine.StatusCode,
                            displayCode = machine.DisplayCode,
                            statusText = machine.StatusText,
                            recipeName = machine.RecipeName,
                            lotNo = machine.LotNo,
                            ec2StatusText = machine.Ec2StatusText,
                            ec2Status = machine.Ec2Status,
                            ec2PartCode = machine.Ec2PartCode,
                            partCode = machine.PartCode,
                            ec2PartColorHex = machine.Ec2PartColorHex,
                            partCodeColorHex = machine.PartCodeColorHex,
                            ec2PartTextColorHex = machine.Ec2PartTextColorHex,
                            partCodeTextColorHex = machine.PartCodeTextColorHex,
                            partCodeDescription = machine.PartCodeDescription,
                            ec2IgnoreReason = machine.Ec2IgnoreReason,
                            isEc2Running = machine.IsEc2Running,
                            isEc2Ignored = machine.IsEc2Ignored,
                            ec2ProcessMinutes = machine.Ec2ProcessMinutes,
                            ec2SettingRate = machine.Ec2SettingRate,
                            ec2OperationRate = machine.Ec2OperationRate,
                            ec2SnapshotAt = machine.Ec2SnapshotAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                            lastUpdate = machine.LastUpdate?.ToString("yyyy-MM-dd HH:mm:ss"),
                            runningMinutes = machine.RunningMinutes,
                            stoppedMinutes = machine.StoppedMinutes,
                            inactiveMinutes = machine.InactiveMinutes,
                            errorMinutes = machine.ErrorMinutes,
                            totalMinutes = machine.TotalMinutes,
                            productionDenominatorMinutes = machine.ProductionDenominatorMinutes,
                            productionPercent = machine.ProductionPercent,
                            enteredAreaAverage = machine.EnteredAreaAverage,
                            areaAverageReason = machine.AreaAverageReason,
                            scheduledOperatorsPt = machine.ScheduledOperatorsPt,
                            scheduledOperatorsJp = machine.ScheduledOperatorsJp
                        }),
                        ranking = dashboard.Ranking.Select(item => new
                        {
                            localId = item.LocalId,
                            localNamePt = item.LocalNamePt,
                            localNameJp = item.LocalNameJp,
                            machineCode = item.MachineCode,
                            machineNamePt = item.MachineNamePt,
                            machineNameJp = item.MachineNameJp,
                            stopMinutes = item.StopMinutes,
                            errorMinutes = item.ErrorMinutes,
                            totalImpactMinutes = item.TotalImpactMinutes
                        }),
                        timeline = dashboard.Timeline.Select(row => new
                        {
                            localId = row.LocalId,
                            sectorId = row.SectorId,
                            localNamePt = row.LocalNamePt,
                            localNameJp = row.LocalNameJp,
                            machineCode = row.MachineCode,
                            lineCode = row.LineCode,
                            machineNamePt = row.MachineNamePt,
                            machineNameJp = row.MachineNameJp,
                            cells = row.Cells.Select(cell => new
                            {
                                timeLabel = cell.TimeLabel,
                                dateTime = cell.DateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                statusCode = cell.StatusCode,
                                displayCode = cell.DisplayCode,
                                isIgnored = cell.IsIgnored,
                                ignoreReason = cell.IgnoreReason,
                                cssClass = cell.CssClass
                            })
                        }),
                        areas = dashboard.Areas.Select(area => new
                        {
                            localId = area.LocalId,
                            sectorId = area.SectorId,
                            localNamePt = area.LocalNamePt,
                            localNameJp = area.LocalNameJp,
                            sectorNamePt = area.SectorNamePt,
                            sectorNameJp = area.SectorNameJp,
                            machineCount = area.MachineCount,
                            machinesRunning = area.MachinesRunning,
                            machinesStopped = area.MachinesStopped,
                            machinesIgnored = area.MachinesIgnored,
                            averageOperatingProcessMinutes = area.AverageOperatingProcessMinutes,
                            runningMinutes = area.RunningMinutes,
                            stoppedMinutes = area.StoppedMinutes,
                            inactiveMinutes = area.InactiveMinutes,
                            errorMinutes = area.ErrorMinutes,
                            totalMinutes = area.TotalMinutes,
                            productionPercent = area.ProductionPercent,
                            lastUpdate = area.LastUpdate?.ToString("yyyy-MM-dd HH:mm:ss"),
                            scheduledOperatorCount = area.ScheduledOperatorCount,
                            scheduledOperatorsPt = area.ScheduledOperatorsPt,
                            scheduledOperatorsJp = area.ScheduledOperatorsJp
                        }),
                        shiftComparisons = dashboard.ShiftComparisons.Select(item => new
                        {
                            shiftId = item.ShiftId,
                            shiftNamePt = item.ShiftNamePt,
                            shiftNameJp = item.ShiftNameJp,
                            start = item.Start.ToString("yyyy-MM-dd HH:mm:ss"),
                            end = item.End.ToString("yyyy-MM-dd HH:mm:ss"),
                            productionPercent = item.ProductionPercent,
                            runningMinutes = item.RunningMinutes,
                            stoppedMinutes = item.StoppedMinutes,
                            inactiveMinutes = item.InactiveMinutes,
                            errorMinutes = item.ErrorMinutes,
                            machineCount = item.MachineCount
                        }),
                        dailyTrend = dashboard.DailyTrend.Select(item => new
                        {
                            date = item.Date.ToString("yyyy-MM-dd"),
                            label = item.Label,
                            shiftId = item.ShiftId,
                            shiftNamePt = item.ShiftNamePt,
                            shiftNameJp = item.ShiftNameJp,
                            productionPercent = item.ProductionPercent,
                            runningMinutes = item.RunningMinutes,
                            stoppedMinutes = item.StoppedMinutes,
                            inactiveMinutes = item.InactiveMinutes,
                            errorMinutes = item.ErrorMinutes
                        }),
                        areaHistory = dashboard.AreaHistory.Select(item => new
                        {
                            localId = item.LocalId,
                            localNamePt = item.LocalNamePt,
                            localNameJp = item.LocalNameJp,
                            days = item.Days.Select(day => new
                            {
                                date = day.Date.ToString("yyyy-MM-dd"),
                                label = day.Label,
                                productionPercent = day.ProductionPercent,
                                runningMinutes = day.RunningMinutes,
                                stoppedMinutes = day.StoppedMinutes,
                                inactiveMinutes = day.InactiveMinutes,
                                errorMinutes = day.ErrorMinutes
                            })
                        }),
                        operatorRanking = dashboard.OperatorRanking.Select(item => new
                        {
                            operatorCodigoFJ = item.OperatorCodigoFJ,
                            operatorNamePt = item.OperatorNamePt,
                            operatorNameJp = item.OperatorNameJp,
                            estimatedRunningMinutes = item.EstimatedRunningMinutes,
                            estimatedKadouritsuPercent = item.EstimatedKadouritsuPercent,
                            fullCoverageDays = item.FullCoverageDays,
                            partialCoverageDays = item.PartialCoverageDays,
                            localNamesPt = item.LocalNamesPt,
                            localNamesJp = item.LocalNamesJp
                        }),
                        gBareruCapacityForecast = MapForecast(dashboard.GBareruCapacityForecast),
                        statuses = LoadStatuses(),
                        partCodeStyles = LoadPartCodeStyles()
                    }
                };
                var json = JsonSerializer.Serialize(payload);
                htmlOrJsonWatch.Stop();

                WriteDashboardPerformanceLog(filter, dashboard, buildWatch.ElapsedMilliseconds, htmlOrJsonWatch.ElapsedMilliseconds);
                PostJsonRaw(json);
            }
            finally
            {
                _databaseOperationGate.Release();
            }
        }

        private async Task ImportAndRefreshAsync(ProductionDashboardFilter filter, bool cleanExistingEvents = false)
        {
            if (_isImportingProduction)
            {
                return;
            }

            _isImportingProduction = true;
            ClearOperatorDetailCache();
            try
            {
                DemandProductionScope(PermissionActions.Edit, 0);
                await _databaseOperationGate.WaitAsync();
                ProductionImportResult result;
                try
                {
                    result = await Task.Run(() => _fileImporter.ImportLatest(new ProductionFileImporter.ProductionImportOptions
                    {
                        CleanExistingEvents = cleanExistingEvents,
                        CleanupFilter = cleanExistingEvents ? filter : null
                    }));
                }
                finally
                {
                    _databaseOperationGate.Release();
                }

                var dashboardRefreshWatch = Stopwatch.StartNew();
                await SendDashboardAsync(ResolvePostImportDashboardFilter(filter));
                dashboardRefreshWatch.Stop();
                result.PerformanceMs["DashboardRefresh"] = dashboardRefreshWatch.ElapsedMilliseconds;

                PostJson(new
                {
                    type = "import_result",
                    data = new
                    {
                        success = true,
                        message = BuildImportMessage(result),
                        filesRead = result.FilesRead,
                        linesRead = result.LinesRead,
                        imported = result.Imported,
                        ignored = result.Ignored,
                        machinesCreated = result.MachinesCreated,
                        cleanupPerformed = result.CleanupPerformed,
                        cleanupEventsDeleted = result.CleanupEventsDeleted,
                        cleanupCurrentStatusesDeleted = result.CleanupCurrentStatusesDeleted,
                        cleanupMessage = result.CleanupMessage,
                        ec2ImportAttempted = result.Ec2ImportAttempted,
                        ec2ImportSkipped = result.Ec2ImportSkipped,
                        ec2ImportMessage = result.Ec2ImportMessage,
                        ec2RowsRead = result.Ec2RowsRead,
                        ec2RowsImported = result.Ec2RowsImported,
                        ec2RowsIgnored = result.Ec2RowsIgnored,
                        dadLinesRead = result.DadLinesRead,
                        dadRowsParsed = result.DadRowsParsed,
                        dadRowsImported = result.DadRowsImported,
                        dadRowsIgnored = result.DadRowsIgnored,
                        dadMachinesFound = result.DadMachinesFound,
                        dadMachinesImported = result.DadMachinesImported,
                        dadMachinesWithRunningEvents = result.DadMachinesWithRunningEvents,
                        dadMachinesWithZeroRunningEvents = result.DadMachinesWithZeroRunningEvents,
                        dadLinkErrors = result.DadLinkErrors,
                        dadEventsByMachine = result.DadEventsByMachine,
                        dadRunningEventsByMachine = result.DadRunningEventsByMachine,
                        dadIgnoredByMachine = result.DadIgnoredByMachine,
                        dadDiagnostics = result.DadDiagnostics,
                        performanceMs = result.PerformanceMs,
                        errors = result.Errors
                    }
                });
            }
            catch (Exception ex)
            {
                PostJson(new
                {
                    type = "error",
                    message = ex.Message
                });
            }
            finally
            {
                _isImportingProduction = false;
            }
        }

        private async Task SendMachineDetailAsync(int machineId)
        {
            if (machineId <= 0)
            {
                PostJson(new
                {
                    type = "machine_detail",
                    data = new
                    {
                        machineId = 0,
                        machineCode = string.Empty,
                        lineCode = string.Empty,
                        events = Array.Empty<object>()
                    }
                });
                return;
            }

            await _databaseOperationGate.WaitAsync();
            try
            {
                var sectorId = GetMachineSectorId(machineId);
                _authorization.Demand(
                    _currentUser.Id,
                    ModuleCodes.Production,
                    PermissionActions.View,
                    sectorId);
                var rows = await Task.Run(() =>
                {
                    using var conn = _factory.CreateOpenConnection();

                    return conn.Query(
                        @"
                            SELECT
                                e.MachineId AS machineId,
                                e.SectorId AS sectorId,
                                e.MachineCode AS machineCode,
                                COALESCE(e.LineCode, '') AS lineCode,
                                COALESCE(m.NamePt, e.MachineCode) AS machineNamePt,
                                COALESCE(NULLIF(m.NameJp, ''), m.NamePt, e.MachineCode) AS machineNameJp,
                                substr(e.EventDateTime, 1, 16) AS eventDateTime,
                                e.StatusCode AS statusCode,
                                COALESCE(e.StatusText, '') AS statusText,
                                COALESCE(e.RecipeName, '') AS recipeName,
                                COALESCE(e.LotNo, '') AS lotNo,
                                COALESCE(e.SourceFile, '') AS sourceFile
                            FROM MachineEvents e
                            LEFT JOIN Machines m ON m.Id = e.MachineId
                            WHERE e.MachineId = @machineId
                            ORDER BY e.EventDateTime DESC, e.Id DESC
                            LIMIT 80;",
                        new
                        {
                            machineId
                        }
                    ).ToList();
                });

                PostJson(new
                {
                    type = "machine_detail",
                    data = new
                    {
                        machineId,
                        machineCode = rows.FirstOrDefault()?.machineCode ?? string.Empty,
                        lineCode = rows.FirstOrDefault()?.lineCode ?? string.Empty,
                        events = rows
                    }
                });
            }
            finally
            {
                _databaseOperationGate.Release();
            }
        }

        private async Task SendOperatorDetailAsync(ProductionDashboardFilter filter, string operatorCodigoFJ)
        {
            DemandProductionScope(PermissionActions.View, filter.SectorId);
            var cacheKey = BuildOperatorDetailCacheKey(filter, operatorCodigoFJ);
            lock (_operatorDetailCacheLock)
            {
                if (_operatorDetailJsonCache.TryGetValue(cacheKey, out var cachedJson))
                {
                    PostJsonRaw(cachedJson);
                    return;
                }
            }

            await _databaseOperationGate.WaitAsync();
            try
            {
                var detail = await Task.Run(() => _analyticsService.GetOperatorDetail(filter, operatorCodigoFJ));

                var payload = new
                {
                    type = "operator_detail",
                    data = new
                    {
                        operatorCodigoFJ = detail.OperatorCodigoFJ,
                        operatorNamePt = detail.OperatorNamePt,
                        operatorNameJp = detail.OperatorNameJp,
                        shiftNamePt = detail.ShiftNamePt,
                        shiftNameJp = detail.ShiftNameJp,
                        averageKadouritsuPercent = detail.AverageKadouritsuPercent,
                        totalRunningMinutes = detail.TotalRunningMinutes,
                        assignedAreaCount = detail.AssignedAreaCount,
                        localNamesPt = detail.LocalNamesPt,
                        localNamesJp = detail.LocalNamesJp,
                        entries = detail.Entries.Select(entry => new
                        {
                            date = entry.Date.ToString("yyyy-MM-dd"),
                            label = entry.Label,
                            localId = entry.LocalId,
                            localNamePt = entry.LocalNamePt,
                            localNameJp = entry.LocalNameJp,
                            runningMinutes = entry.RunningMinutes,
                            stoppedMinutes = entry.StoppedMinutes,
                            inactiveMinutes = entry.InactiveMinutes,
                            errorMinutes = entry.ErrorMinutes,
                            eligibleMinutes = entry.EligibleMinutes,
                            kadouritsuPercent = entry.KadouritsuPercent,
                            coverageMode = entry.CoverageMode,
                            isPartialCoverage = entry.IsPartialCoverage,
                            effectiveMinutes = entry.EffectiveMinutes,
                            plannedMinutes = entry.PlannedMinutes,
                            eventsCount = entry.EventsCount,
                            countsInAverage = entry.CountsInAverage,
                            averageReason = entry.AverageReason,
                            windowStart = entry.WindowStart?.ToString("yyyy-MM-dd HH:mm:ss"),
                            windowEnd = entry.WindowEnd?.ToString("yyyy-MM-dd HH:mm:ss")
                        })
                    }
                };
                var json = JsonSerializer.Serialize(payload);

                lock (_operatorDetailCacheLock)
                {
                    _operatorDetailJsonCache[cacheKey] = json;
                }

                PostJsonRaw(json);
            }
            finally
            {
                _databaseOperationGate.Release();
            }
        }

        private static string BuildOperatorDetailCacheKey(ProductionDashboardFilter filter, string operatorCodigoFJ)
        {
            return string.Join(
                "|",
                filter.Date.Date.ToString("yyyy-MM-dd"),
                filter.ShiftId,
                filter.SectorId,
                filter.LocalId,
                filter.MachineId,
                filter.MachineCode?.Trim() ?? string.Empty,
                operatorCodigoFJ?.Trim() ?? string.Empty);
        }

        private void ClearOperatorDetailCache()
        {
            lock (_operatorDetailCacheLock)
            {
                _operatorDetailJsonCache.Clear();
            }
        }

        private static string BuildImportMessage(ProductionImportResult result)
        {
            var message = $"{L("Arquivos", "\u30d5\u30a1\u30a4\u30eb")}: {result.FilesRead} | {L("Linhas", "\u884c")}: {result.LinesRead} | {L("Importadas", "\u53d6\u8fbc\u6e08\u307f")}: {result.Imported} | {L("Ignoradas", "\u9664\u5916")}: {result.Ignored}";

            if (result.MachinesCreated > 0)
            {
                message += $" | {L("Maquinas novas", "\u65b0\u898f\u8a2d\u5099")}: {result.MachinesCreated}";
            }

            if (result.PlanFilesRead > 0)
            {
                message += $" | DAT: {result.PlanFilesRead} {L("arquivo(s)", "\u30d5\u30a1\u30a4\u30eb")}, {result.PlanRowsImported} {L("linha(s) de plano", "\u8a08\u753b\u884c")}";
                if (result.PlanRowsIgnored > 0)
                {
                    message += $", {result.PlanRowsIgnored} {L("ignorada(s)", "\u9664\u5916")}";
                }
            }

            if (result.DadLinesRead > 0 || result.DadRowsParsed > 0 || result.DadRowsImported > 0 || result.DadRowsIgnored > 0)
            {
                message += $" | DAD: linhas={result.DadLinesRead}, maquinas={result.DadMachinesFound}, importadas={result.DadMachinesImported}, sem rodando={result.DadMachinesWithZeroRunningEvents}, erros vinculo={result.DadLinkErrors}";
            }

            if (result.CleanupPerformed)
            {
                message += $" | {L("Limpeza", "\u30af\u30ea\u30fc\u30f3\u30a2\u30c3\u30d7")}: {result.CleanupEventsDeleted} {L("evento(s)", "\u30a4\u30d9\u30f3\u30c8")}, {result.CleanupCurrentStatusesDeleted} {L("status atual(is)", "\u73fe\u5728\u30b9\u30c6\u30fc\u30bf\u30b9")}";
            }

            if (result.BatchExecuted && !string.IsNullOrWhiteSpace(result.BatchMessage))
            {
                message += $" | BAT: {result.BatchMessage}";
            }

            if (result.Ec2ImportAttempted || !string.IsNullOrWhiteSpace(result.Ec2ImportMessage))
            {
                message += $" | {result.Ec2ImportMessage}";
                if (result.Ec2RowsRead > 0)
                {
                    message += $" ({result.Ec2RowsImported}/{result.Ec2RowsRead})";
                }
            }

            if (result.PerformanceMs.Count > 0)
            {
                message += " | Perf: " + string.Join(
                    ", ",
                    result.PerformanceMs
                        .Where(item => item.Key is "Batch" or "DiscoverFiles" or "ReadFiles" or "Parse" or "OpenConnection" or "Cleanup" or "LoadMachines" or "LoadStatuses" or "DuplicateCheck" or "InsertEvents" or "Commit" or "Ec2Import" or "Ec2Total" or "DashboardRefresh" or "Total")
                        .Select(item => $"{item.Key}={item.Value}ms"));
            }

            return message;
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }

        private ProductionDashboardFilter ResolvePostImportDashboardFilter(ProductionDashboardFilter filter)
        {
            using var conn = _factory.CreateOpenConnection();

            var selectedDateHasEvents = conn.ExecuteScalar<int>(
                @"
                    SELECT COUNT(1)
                    FROM MachineEvents
                    WHERE EventDateTime >= @start
                      AND EventDateTime < @end;",
                new
                {
                    start = filter.Date.Date.ToString("yyyy-MM-dd HH:mm:ss"),
                    end = filter.Date.Date.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss")
                }) > 0;

            if (selectedDateHasEvents)
            {
                return filter;
            }

            var latestEventDate = conn.ExecuteScalar<string>(
                @"
                    SELECT substr(MAX(EventDateTime), 1, 10)
                    FROM MachineEvents;"
            );

            if (DateTime.TryParse(latestEventDate, out var parsedLatestDate))
            {
                filter.Date = parsedLatestDate.Date;
            }

            return filter;
        }

        private object LoadStatuses()
        {
            using var conn = _factory.CreateOpenConnection();

            return LoadStatuses(conn);
        }

        private object LoadPartCodeStyles()
        {
            using var conn = _factory.CreateOpenConnection();

            return conn.Query(
                @"
                    SELECT
                        PartCode AS partCode,
                        COALESCE(ColorHex, '#D93F3F') AS colorHex,
                        COALESCE(TextColorHex, '#FFFFFF') AS textColorHex,
                        COALESCE(Description, '') AS description
                    FROM ProductionPartCodeStyles
                    WHERE COALESCE(IsActive, 1) = 1
                    ORDER BY PartCode;"
            ).ToList();
        }

        private static object MapForecast(GBareruCapacityForecastDto forecast)
        {
            return new
            {
                isAvailable = forecast.IsAvailable,
                message = forecast.Message,
                eciiMinutes = forecast.EciiMinutes,
                bunkatsuMinutes = forecast.BunkatsuMinutes,
                dcsMinutes = forecast.DcsMinutes,
                peopleCount = forecast.PeopleCount,
                cycleMode = forecast.CycleMode,
                cycleMinutes = forecast.CycleMinutes,
                block1Minutes = forecast.Block1Minutes,
                block2Minutes = forecast.Block2Minutes,
                bottleneck = forecast.Bottleneck,
                availableMinutes = forecast.AvailableMinutes,
                forecastCapacity = forecast.ForecastCapacity,
                forecastKadouritsuPercent = forecast.ForecastKadouritsuPercent,
                realKadouritsuPercent = forecast.RealKadouritsuPercent,
                differencePercent = forecast.DifferencePercent,
                calculationMs = forecast.CalculationMs,
                areas = forecast.Areas.Select(area => new
                {
                    localId = area.LocalId,
                    localNamePt = area.LocalNamePt,
                    localNameJp = area.LocalNameJp,
                    peopleCount = area.PeopleCount,
                    cycleMode = area.CycleMode,
                    cycleMinutes = area.CycleMinutes,
                    forecastCapacity = area.ForecastCapacity,
                    forecastKadouritsuPercent = area.ForecastKadouritsuPercent,
                    realKadouritsuPercent = area.RealKadouritsuPercent,
                    message = area.Message
                })
            };
        }

        private static object LoadStatuses(System.Data.IDbConnection conn)
        {
            return conn.Query(
                @"
                    SELECT
                        COALESCE(SectorId, 0) AS sectorId,
                        StatusCode AS statusCode,
                        DisplayCode AS displayCode,
                        COALESCE(Classification, '') AS classification,
                        COALESCE(NamePt, '') AS namePt,
                        COALESCE(NULLIF(NameJp, ''), NamePt, '') AS nameJp,
                        COALESCE(ColorHex, '#5B88E8') AS colorHex,
                        COALESCE(TextColorHex, '#FFFFFF') AS textColorHex
                    FROM MachineStatuses
                    WHERE COALESCE(IsActive, 1) = 1
                    ORDER BY SortOrder, StatusCode;"
            ).ToList();
        }

        private ProductionDashboardFilter ReadFilter(JsonElement root)
        {
            return new ProductionDashboardFilter
            {
                Date = ReadDate(root, "date", DateTime.Today),
                ShiftId = ReadInt(root, "shiftId", _currentOperator.ShiftId),
                SectorId = ReadInt(root, "sectorId", 0),
                LocalId = ReadInt(root, "localId", 0),
                MachineId = ReadInt(root, "machineId", 0),
                MachineCode = ReadString(root, "machineCode").Trim()
            };
        }

        private void DemandProductionScope(string action, int sectorId)
        {
            _authorization.DemandSectorOrAggregate(
                _currentUser.Id,
                ModuleCodes.Production,
                action,
                sectorId);

            if (sectorId > 0)
                return;

            var authorized = _authorization.GetContext(_currentUser.Id).AuthorizedSectorIds;
            using var conn = _factory.CreateOpenConnection();
            var allActive = conn.Query<int>("SELECT Id FROM Sectors WHERE IsActive = 1;").ToHashSet();
            if (!allActive.SetEquals(authorized))
                throw new UnauthorizedAccessException(L(
                    "Selecione um setor explicito para este escopo.",
                    "この範囲ではセクターを明示的に選択してください。"));
        }

        private int GetMachineSectorId(int machineId)
        {
            using var conn = _factory.CreateOpenConnection();
            var sectorId = conn.ExecuteScalar<int?>(
                "SELECT SectorId FROM Machines WHERE Id = @machineId AND COALESCE(IsActive, 1) = 1;",
                new { machineId });
            return sectorId ?? throw new InvalidOperationException(L(
                "Maquina nao encontrada.",
                "設備が見つかりません。"));
        }

        private void PostJson(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            PostJsonRaw(json);
        }

        private void PostJsonRaw(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    if (webViewProductionMonitor.CoreWebView2 != null)
                    {
                        webViewProductionMonitor.CoreWebView2.PostWebMessageAsJson(json);
                    }
                }));
                return;
            }

            if (webViewProductionMonitor.CoreWebView2 != null)
            {
                webViewProductionMonitor.CoreWebView2.PostWebMessageAsJson(json);
            }
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString() ?? string.Empty
                : string.Empty;
        }

        private static void LogDashboardDiagnostics(ProductionDashboardDto dashboard)
        {
            foreach (var machine in dashboard.Machines)
            {
                WriteDiagnostic(
                    $"[ProductionMonitor][MachinePayload] Machine={machine.MachineCode} Equipment={machine.MachineNamePt} Lot={machine.LotNo} Code={machine.Ec2PartCode} Time={FormatNullableDouble(machine.Ec2ProcessMinutes)} Percent={machine.ProductionPercent.ToString("0.0")} Status={machine.StatusText} PartColor={machine.Ec2PartColorHex} TextColor={machine.Ec2PartTextColorHex}");
            }
        }

        private static void WriteDashboardPerformanceLog(
            ProductionDashboardFilter filter,
            ProductionDashboardDto dashboard,
            long buildDashboardMs,
            long buildHtmlOrJsonMs)
        {
            var rowsRead = dashboard.Machines.Count
                + dashboard.Areas.Count
                + dashboard.Ranking.Count
                + dashboard.Timeline.Count
                + dashboard.ShiftComparisons.Count
                + dashboard.DailyTrend.Count
                + dashboard.AreaHistory.Count
                + dashboard.OperatorRanking.Count;

            WriteDiagnostic(
                "[ProductionMonitor][DashboardPerformance] " +
                $"Action=BuildDashboard " +
                $"Date={filter.Date:yyyy-MM-dd} " +
                $"Shift={filter.ShiftId} " +
                $"Sector={filter.SectorId} " +
                $"Local={filter.LocalId} " +
                $"Machine={filter.MachineId} " +
                $"BuildDashboardMs={buildDashboardMs} " +
                $"BuildHtmlOrJsonMs={buildHtmlOrJsonMs} " +
                $"RowsRead={rowsRead} " +
                $"MachinesCount={dashboard.Machines.Count} " +
                $"AreasCount={dashboard.Areas.Count} " +
                $"TotalMs={buildDashboardMs + buildHtmlOrJsonMs}");
        }

        private static string FormatNullableDouble(double? value)
        {
            if (!value.HasValue)
            {
                return "null";
            }

            return double.IsFinite(value.Value)
                ? value.Value.ToString("0.0")
                : value.Value.ToString();
        }

        private static void WriteDiagnostic(string message)
        {
            Debug.WriteLine(message);

            try
            {
                Console.WriteLine(message);
            }
            catch
            {
            }
        }

        private static int ReadInt(JsonElement root, string propertyName, int fallback = 0)
        {
            if (!root.TryGetProperty(propertyName, out var prop))
            {
                return fallback;
            }

            return prop.ValueKind switch
            {
                JsonValueKind.Number => prop.GetInt32(),
                JsonValueKind.String when int.TryParse(prop.GetString(), out var parsed) => parsed,
                _ => fallback
            };
        }

        private static DateTime ReadDate(JsonElement root, string propertyName, DateTime fallback)
        {
            if (!root.TryGetProperty(propertyName, out var prop))
            {
                return fallback.Date;
            }

            var text = prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;

            return DateTime.TryParse(text, out var parsed)
                ? parsed.Date
                : fallback.Date;
        }
    }
}
