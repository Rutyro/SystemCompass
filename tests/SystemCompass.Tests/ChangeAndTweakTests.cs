using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCompass.Core.Changes;
using SystemCompass.Core.Startup;
using SystemCompass.Core.Tweaks;

namespace SystemCompass.Tests;

[TestClass]
public sealed class ChangeAndTweakTests
{
    [TestMethod]
    public void DisableIsVerifiedAndCanBeRolledBack()
    {
        var entry = TestData.Entry("App");
        var provider = new FakeStartup(entry);
        var journal = new InMemoryJournal();
        var executor = new ChangeExecutor([provider], journal, TimeProvider.System);

        var result = executor.SetState(entry, false);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(EntryState.Disabled, result.ObservedState);
        Assert.AreEqual(EntryState.Disabled, provider.Enumerate()[0].State);

        var rollback = executor.Rollback(result.ChangeId);

        Assert.IsTrue(rollback.Success);
        Assert.AreEqual(EntryState.Enabled, provider.Enumerate()[0].State);
    }

    [TestMethod]
    public void VerificationFailureIsReportedAndJournaled()
    {
        var entry = TestData.Entry("Stubborn");
        var provider = new FakeStartup(entry) { IgnoreChanges = true };
        var journal = new InMemoryJournal();
        var executor = new ChangeExecutor([provider], journal, TimeProvider.System);

        var result = executor.SetState(entry, false);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(ChangeStatus.Failed, JournalView.LatestPerChange(journal.ReadAll())[0].Status);
        Assert.IsNotNull(result.Message);
    }

    [TestMethod]
    public void TweakApplyAndRevertRestoreRegistryValue()
    {
        var tweak = new TweakDefinition(
            "test-tweak", "Test", "Test tweak", "Test", TweakEvidence.Community, [],
            TweakRisk.Low, 10240, 99999999, false, false,
            [new RegistrySet("HKCU", "Software\\Test", "Value", RegistryValueKindKind.DWord, "1")]);
        var backend = new InMemoryTweaks();
        backend.Values["HKCU\\Software\\Test\\Value"] = new TweakValue(RegistryValueKindKind.DWord, "0");
        var journal = new InMemoryJournal();
        var engine = new TweakEngine(backend, journal, TimeProvider.System);

        Assert.AreEqual(TweakStatus.NotApplied, engine.GetStatus(tweak, 26100));

        var applied = engine.Apply(tweak, 26100);
        Assert.IsTrue(applied.Success);
        Assert.AreEqual("1", backend.Values["HKCU\\Software\\Test\\Value"].Data);

        var reverted = engine.Revert(tweak);
        Assert.IsTrue(reverted.Success);
        Assert.AreEqual("0", backend.Values["HKCU\\Software\\Test\\Value"].Data);
    }

    [TestMethod]
    public void TcpGlobalApplyAndRevertRestorePreviousValue()
    {
        var tweak = new TweakDefinition(
            "test-tcp", "Test", "Test tweak", "Test", TweakEvidence.Community, [],
            TweakRisk.Low, 10240, 99999999, true, false,
            [new TcpGlobal("autotuninglevel", "disabled")]);
        var backend = new InMemoryTweaks();
        backend.TcpGlobals["autotuninglevel"] = "normal";
        var engine = new TweakEngine(backend, new InMemoryJournal(), TimeProvider.System);

        Assert.AreEqual(TweakStatus.NotApplied, engine.GetStatus(tweak, 26100));
        Assert.IsTrue(engine.Apply(tweak, 26100).Success);
        Assert.AreEqual("disabled", backend.TcpGlobals["autotuninglevel"]);
        Assert.IsTrue(engine.Revert(tweak).Success);
        Assert.AreEqual("normal", backend.TcpGlobals["autotuninglevel"]);
    }

    [TestMethod]
    public void ScheduledTaskApplyAndRevertRestorePreviousState()
    {
        const string path = "\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator";
        var tweak = new TweakDefinition(
            "test-task", "Test", "Test tweak", "Test", TweakEvidence.Community, [],
            TweakRisk.Low, 10240, 99999999, true, false,
            [new ScheduledTaskState(path, false)]);
        var backend = new InMemoryTweaks();
        backend.ScheduledTasks[path] = true;
        var engine = new TweakEngine(backend, new InMemoryJournal(), TimeProvider.System);

        Assert.AreEqual(TweakStatus.NotApplied, engine.GetStatus(tweak, 26100));
        Assert.IsTrue(engine.Apply(tweak, 26100).Success);
        Assert.IsFalse(backend.ScheduledTasks[path]);
        Assert.IsTrue(engine.Revert(tweak).Success);
        Assert.IsTrue(backend.ScheduledTasks[path]);
    }

    [TestMethod]
    public void DenyListRejectsArbitraryNetshAndUpdateTasks()
    {
        Assert.IsTrue(SecurityDenyList.IsDenied(new TcpGlobal("winsock", "reset")));
        Assert.IsTrue(SecurityDenyList.IsDenied(
            new ScheduledTaskState("\\Microsoft\\Windows\\WindowsUpdate\\Automatic App Update", false)));
        Assert.IsTrue(SecurityDenyList.IsDenied(new RegistrySet(
            "HKLM", "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System",
            "EnableLUA", RegistryValueKindKind.DWord, "0")));
        Assert.IsFalse(SecurityDenyList.IsDenied(new TcpGlobal("autotuninglevel", "normal")));
    }

    [TestMethod]
    public void SecurityProtectedTweakIsRefused()
    {
        var tweak = new TweakDefinition(
            "defender-test", "Test", "Test", "Test", TweakEvidence.Community, [],
            TweakRisk.High, 10240, 99999999, true, false,
            [new RegistrySet("HKLM", "SOFTWARE\\Policies\\Microsoft\\Windows\\Defender", "DisableAntiSpyware",
                RegistryValueKindKind.DWord, "1")]);
        var engine = new TweakEngine(new InMemoryTweaks(), new InMemoryJournal(), TimeProvider.System);

        var result = engine.Apply(tweak, 26100);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(TweakStatus.NotApplicable, result.Status);
    }

    [TestMethod]
    public void EmbeddedCatalogPassesValidation()
    {
        var problems = TweakCatalog.FromEmbedded().Validate();

        Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void SnapshotComparerFindsNewReEnabledAndRemoved()
    {
        var now = DateTimeOffset.UnixEpoch;
        var previous = new Snapshot(now, [
            new SnapshotEntry("a", "A", "RunKey", "Enabled"),
            new SnapshotEntry("b", "B", "RunKey", "Disabled"),
            new SnapshotEntry("c", "C", "RunKey", "Enabled"),
        ]);
        var current = new Snapshot(now, [
            new SnapshotEntry("b", "B", "RunKey", "Enabled"),
            new SnapshotEntry("d", "D", "RunKey", "Enabled"),
        ]);

        var changes = SnapshotComparer.Compare(previous, current);

        Assert.AreEqual(SnapshotChangeKind.ReEnabled, changes.Single(change => change.Entry.Id == "b").Kind);
        Assert.AreEqual(SnapshotChangeKind.New, changes.Single(change => change.Entry.Id == "d").Kind);
        Assert.AreEqual(SnapshotChangeKind.Removed, changes.Single(change => change.Entry.Id == "a").Kind);
    }

    private sealed class FakeStartup : ManageStartup
    {
        private readonly List<StartupEntry> _entries;

        public FakeStartup(params StartupEntry[] entries)
        {
            _entries = entries.ToList();
        }

        public StartupMechanism Mechanism => StartupMechanism.RunKey;

        public bool CanSetState => true;

        public bool IgnoreChanges { get; init; }

        public IReadOnlyList<StartupEntry> Enumerate() => _entries.ToArray();

        public void SetState(string entryId, bool enabled)
        {
            if (IgnoreChanges)
            {
                return;
            }

            var index = _entries.FindIndex(entry => entry.Id == entryId);
            _entries[index] = _entries[index] with
            {
                State = enabled ? EntryState.Enabled : EntryState.Disabled,
            };
        }
    }

    private sealed class InMemoryTweaks : ApplyTweaks
    {
        public Dictionary<string, TweakValue> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> TcpGlobals { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, bool> ScheduledTasks { get; } = new(StringComparer.OrdinalIgnoreCase);

        public TweakValue? ReadRegistry(RegistryTarget target)
            => Values.GetValueOrDefault(Key(target));

        public void WriteRegistry(RegistryTarget target, TweakValue value) => Values[Key(target)] = value;

        public void DeleteRegistry(RegistryTarget target) => Values.Remove(Key(target));

        public ServiceStartModeKind? ReadServiceStart(string serviceName) => null;

        public void SetServiceStart(string serviceName, ServiceStartModeKind mode)
        {
        }

        public uint? ReadPowerSetting(Guid subGroup, Guid setting, bool ac) => null;

        public void WritePowerSetting(Guid subGroup, Guid setting, bool ac, uint value)
        {
        }

        public string? ReadTcpGlobal(string name) => TcpGlobals.GetValueOrDefault(name);

        public void WriteTcpGlobal(string name, string value) => TcpGlobals[name] = value;

        public bool? ReadScheduledTaskEnabled(string taskPath)
            => ScheduledTasks.TryGetValue(taskPath, out var enabled) ? enabled : null;

        public void SetScheduledTaskEnabled(string taskPath, bool enabled)
            => ScheduledTasks[taskPath] = enabled;

        private static string Key(RegistryTarget target) => $"{target.Hive}\\{target.KeyPath}\\{target.ValueName}";
    }
}
