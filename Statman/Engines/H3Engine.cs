﻿using System;
using System.Diagnostics;
using Statman.Engines.H3;
using Statman.Engines.H3.Controls;
using Statman.Util;
using Statman.Util.Injection;

namespace Statman.Engines
{
    class H3Engine : IEngine
    {
        public bool Active { get; private set; }
        public ProcessMemoryReader Reader { get; private set; }

        public StatTracker StatTracker { get; private set; }
        public TimeTracker TimeTracker { get; private set; }
        public SceneTracker SceneTracker { get; private set; }

        public MainControl Control { get; private set; }

        private Process m_GameProcess;
        private Injector m_Injector;

        private uint m_SkipUpdates;

        public H3Engine()
        {
            Active = false;
        }

        public void Update()
        {
            if (m_GameProcess == null || m_GameProcess.HasExited)
            {
                if (m_SkipUpdates > 0)
                {
                    --m_SkipUpdates;
                    return;
                }

                m_SkipUpdates = 16;

                m_GameProcess = null;
                Active = false;

                if (Reader != null)
                {
                    Reader.CloseHandle();
                    Reader = null;
                }

                if (m_Injector != null)
                {
                    m_Injector.Dispose();
                    m_Injector = null;
                }

                var s_Processes = Process.GetProcessesByName("HitmanBloodMoney");

                if (s_Processes.Length == 0)
                    return;

                // We always select the first process.
                m_GameProcess = s_Processes[0];

                // Setup our Memory Reader.
                Reader = new ProcessMemoryReader(m_GameProcess);

                try
                {
                    if (Reader.OpenProcess())
                    {
                        m_SkipUpdates = 0;
                        Active = true;

                        // Create our injector and inject our stat module.
                        m_Injector = new Injector(m_GameProcess, true);
                        m_Injector.InjectLibrary("H3.dll");

                        // Setup our main control.
                        MainApp.MainWindow.Dispatcher.Invoke((Action) (() =>
                        {
                            Control = new MainControl();
                        }));

                        // Setup our engine-specific classes.
                        StatTracker = new StatTracker(this);
                        TimeTracker = new TimeTracker(this);
                        SceneTracker = new SceneTracker(this);

                        // Set our control in the main window.
                        MainApp.MainWindow.SetEngineControl(Control);
                    }
                }
                catch (Exception)
                {
                    m_GameProcess = null;
                    Active = false;
                }
            }

            if (!Active)
                return;

            SceneTracker.Update();
            TimeTracker.Update();
            StatTracker.Update();

            if (StatTracker.CurrentStats.m_Time > 0 || !SceneTracker.InGame)
                Control.SetCurrentTime(StatTracker.CurrentStats.m_Time);
            else
                Control.SetCurrentTime(TimeTracker.CurrentTime);
        }
    }
}