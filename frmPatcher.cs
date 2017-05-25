﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using XMplayerBasedOnKurapicaMapper;

namespace DifferentSLIAuto
{
    public partial class frmPatcher : Form
    {
        /// <summary>
        /// m_vFileToPatch
        /// 
        ///     The contents of the file to be patched.
        /// </summary>
        private byte[] m_vFileToPatch;
        private string m_DriverFile;

        private ListBoxLog listBoxLog;

        internal enum uFMOD_Flags
        {
            XM_RESOURCE = 0,
            XM_MEMORY = 1,
            XM_FILE = 2,
            XM_NOLOOP = 8,
            XM_SUSPENDED = 16
        }

        internal delegate IntPtr PlaySong(IntPtr lpXM, int param, uFMOD_Flags fdwSong);
        internal delegate IntPtr SetVolume(int vol);

        public frmPatcher()
        {
            InitializeComponent();
            listBoxLog = new ListBoxLog(_listBox);
        }

        private void frmPatcher_Load(object sender, EventArgs e)
        {
            listBoxLog.Log(ListBoxLog.Level.Info, string.Format("Welcome to {0} ,please read instructions carefully", this.Text));
            listBoxLog.Log(ListBoxLog.Level.Info, string.Format("Press \"{0}\" to patch your driver.", btnPatch.Text));

            byte[] dat = Properties.Resources.ufmod;
            DynamicDllLoader loader = new DynamicDllLoader();

            // Load DLL
            bool loaded = loader.LoadLibrary(dat);
            if (!loaded)
            {
                listBoxLog.Log(ListBoxLog.Level.Warning, "Could not load uFMOD to play chiptune.");
                return;
            }

            // Get method Address
            uint addrSetVolume = loader.GetProcAddress("uFMOD_SetVolume");
            uint addrPlaySong = loader.GetProcAddress("uFMOD_PlaySong");

            using (UnmanagedMemoryStream xm = (UnmanagedMemoryStream)Assembly.GetEntryAssembly().GetManifestResourceStream("DifferentSLIAuto.plastic.xm"))
            {
                if (xm == null)
                {
                    listBoxLog.Log(ListBoxLog.Level.Warning, "Could not load XM resource to play chiptune.");
                    return;
                }

                SetVolume invokeSetVolume = (SetVolume)Marshal.GetDelegateForFunctionPointer((IntPtr)addrSetVolume, typeof(SetVolume));
                PlaySong invokePlaySong = (PlaySong)Marshal.GetDelegateForFunctionPointer((IntPtr)addrPlaySong, typeof(PlaySong));

                unsafe
                {
                    invokeSetVolume(19);
                    invokePlaySong((IntPtr)xm.PositionPointer, (int)xm.Length, uFMOD_Flags.XM_MEMORY);
                }
            }
        }

        private void btnPatch_Click(object sender, EventArgs e)
        {
            btnPatch.Enabled = false;
            Thread thread = new Thread(PatcherThread);
            thread.IsBackground = true;
            thread.Start();
        }

        private readonly byte[][][] patch5To10Bytes =
        {
            new [] { new byte[] { 0x0F, 0xBA, 0x00, 0x11 }, new byte[] { 0x0F, 0xBA, 0x00, 0x18 }, new byte[] { 0x0F, 0xBA, 0x00, 0x19 }, new byte[] { 0x0F, 0xBA, 0x00, 0x0B },  new byte[] { 0x0F, 0xBA, 0x00, 0x0C }, new byte[] { 0x0F, 0xBA, 0x00, 0x10 } },
            new [] { new byte[] { 0x0F, 0xBA, 0x00, 0x00, 0x11 }, new byte[] { 0x0F, 0xBA, 0x00, 0x00, 0x18 }, new byte[] { 0x0F, 0xBA, 0x00, 0x00, 0x19 }, new byte[] { 0x0F, 0xBA, 0x00, 0x00, 0x0B },  new byte[] { 0x0F, 0xBA, 0x00, 0x00, 0x0C }, new byte[] { 0x0F, 0xBA, 0x00, 0x00, 0x10 } },
            new [] { new byte[] { 0x0F, 0xBA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x11 }, new byte[] { 0x0F, 0xBA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x18 }, new byte[] { 0x0F, 0xBA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x19 }, new byte[] { 0x0F, 0xBA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0B },  new byte[] { 0x0F, 0xBA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C }, new byte[] { 0x0F, 0xBA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10 } }
        };

        private void PatcherThread()
        {
            int[] patches = new int[10];
            int[] patchPerms = new int[10];
            int patchTemp = 0;

            if (!File.Exists("nvlddmkm.sys"))
            {
                if (!File.Exists("nvlddmkm2.sys"))
                {
                    listBoxLog.Log(ListBoxLog.Level.Error, "Could not find \"nvlddmkm.sys\" or \"nvlddmkm2.sys,\" please place it in the patcher directory.");
                    btnPatch.Enabled = true;
                    return;
                }
                else
                {
                    m_DriverFile = "nvlddmkm2.sys";
                }
            }
            else
            {
                m_DriverFile = "nvlddmkm.sys";
            }
            if ((patches[0] = FindPattern(new byte[] { 0x74, 0x00, 0xFE, 0x81, 0x00, 0x00, 0x00, 0x00, 0x0F, 0xBE, 0x81 }, "x?xx????xxx", 0x600)) == -1)
                listBoxLog.Log(ListBoxLog.Level.Error, "Could not find patch #1.");
            if ((patches[1] = FindPattern(new byte[] { 0x74, 0x00, 0xFE, 0x81, 0x00, 0x00, 0x00, 0x00, 0x0F, 0xBE, 0x81 }, "x?xx????xxx", patches[0] + 0xA)) == -1)
                if ((patches[1] = FindPattern(new byte[] { 0x74, 0x00, 0xFE, 0x81, 0x00, 0x00, 0x00, 0x00, 0xB1, 0x7D, 0xA5 }, "x?xx????xxx", patches[0] + 0xA)) == -1) //382.xx
                    listBoxLog.Log(ListBoxLog.Level.Error, "Could not find patch #2.");
            if ((patches[2] = FindPattern(new byte[] { 0x85, 0xC0, 0x74, 0x00, 0x41, 0x0F, 0xBA, 0x2C, 0x24, 0x0F }, "xxx?xxxxxx", 0x600)) == -1)
                if ((patches[2] = FindPattern(new byte[] { 0x85, 0xC0, 0x74, 0x00, 0x41, 0x0F, 0xBA, 0x2F, 0X0F }, "xxx?xxxxx", 0x600)) == -1)
                    listBoxLog.Log(ListBoxLog.Level.Error, "Could not find patch #3.");
            if ((patches[3] = FindPattern(new byte[] { 0x75, 0x00, 0x0F, 0xBA, 0xE8, 0x00, 0x89, 0x45, 0x00, 0x85, 0xC0, 0x0F, 0x85, 0x00, 0x00, 0x00, 0x00, 0x85, 0xDB, 0x0F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x33 }, "x?xxx?xx?xxxx????xxxx????x", 0x600)) == -1)
            {
                if ((patches[3] = FindPattern(new byte[] { 0x75, 0x00, 0x0F, 0xBA, 0x6D, 0x00, 0x0E, 0x85, 0xDB, 0x0F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x33 }, "x?xxx?xxxxx????x", 0x600)) == -1)
                {
                    if ((patches[3] = FindPattern(new byte[] { 0x75, 0x00, 0x0F, 0xBA, 0xAC, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x85, 0xF6, 0x0F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x33 }, "x?xxx??????xxxx????x", 0x600)) == -1)
                    {
                        if ((patches[3] = FindPattern(new byte[] { 0x75, 0x07, 0x0F, 0xBA, 0x00, 0x0E, 0x89, 0x00, 0x00, 0x85, 0x00, 0x0F, 0x85, 0x00, 0x00, 0x00, 0x00, 0x85, 0x00, 0x0F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x44 }, "xxxx?xx??x?xx????x?xx????x", 0x600)) == -1) //337.50+
                        {
                            if ((patches[3] = FindPattern(new byte[] { 0x75, 0x05, 0x0F, 0xBA, 0x6D, 0x00, 0x00, 0x85, 0xF6, 0x0F, 0x84 }, "xxxxx??xxxx", 0x600)) == -1) //307.32
                            {
                                if ((patches[3] = FindPattern(new byte[] { 0x75, 0x3f, 15, 0xba, 0xea, 14, 0x89, 0x55, 0x3f, 0x85, 210, 15, 0x85, 0x3f, 0x3f, 0x3f, 0x3f, 0x85, 0xdb, 15, 0x84, 0x3f, 0x3f, 0x3f, 0x3f }, "x?xxxxxx?xxxx????xxxx????", 0x600)) == -1)
                                {
                                    listBoxLog.Log(ListBoxLog.Level.Error, "Could not find patch #4.");
                                }
                                else patchPerms[3] = 3;
                            }
                            else patchPerms[3] = 4;
                        }
                        else patchPerms[3] = 3;
                    }
                    else patchPerms[3] = 2;
                }
                else patchPerms[3] = 1;
            }
            else patchPerms[3] = 0;

            if (patches[3] != -1)
            {
                for (int x = 4; x <= 9; x++)
                {
                    if ((patchTemp = FindPattern(patch5To10Bytes[0][x - 4], "xx?x", patches[x - 1])) != -1 && (patchTemp - patches[x - 1] < 0x200))
                    {
                        patches[x] = patchTemp;
                        patchPerms[x] = 0;
                    }
                    else if ((patchTemp = FindPattern(patch5To10Bytes[1][x - 4], "xx??x", patches[x - 1])) != -1 && (patchTemp - patches[x - 1] < 0x200))
                    {
                        patches[x] = patchTemp;
                        patchPerms[x] = 1;
                    }
                    else if ((patchTemp = FindPattern(patch5To10Bytes[2][x - 4], "xx??????x", patches[x - 1])) != -1 && (patchTemp - patches[x - 1] < 0x200))
                    {
                        patches[x] = patchTemp;
                        patchPerms[x] = 2;
                    }
                    else
                    {
                        listBoxLog.Log(ListBoxLog.Level.Error, string.Format("Could not find patch #{0}.", x + 1));
                        patches[x] = -1;
                    }
                }
            }

            if (!patches.Any(t => t == -1))
            {
                m_vFileToPatch[patches[0] + 2] = 0x90;
                m_vFileToPatch[patches[0] + 3] = 0x90;
                m_vFileToPatch[patches[0] + 4] = 0x90;
                m_vFileToPatch[patches[0] + 5] = 0x90;
                m_vFileToPatch[patches[0] + 6] = 0x90;
                m_vFileToPatch[patches[0] + 7] = 0x90;
                
                m_vFileToPatch[patches[1] + 2] = 0x90;
                m_vFileToPatch[patches[1] + 3] = 0x90;
                m_vFileToPatch[patches[1] + 4] = 0x90;
                m_vFileToPatch[patches[1] + 5] = 0x90;
                m_vFileToPatch[patches[1] + 6] = 0x90;
                m_vFileToPatch[patches[1] + 7] = 0x90;

                m_vFileToPatch[patches[2]] = 0x31;

                if (patchPerms[3] == 0)
                {
                    m_vFileToPatch[patches[3]] = 0x90;
                    m_vFileToPatch[patches[3] + 1] = 0x90;
                    m_vFileToPatch[patches[3] + 2] = 0x90;
                    m_vFileToPatch[patches[3] + 3] = 0x90;
                    m_vFileToPatch[patches[3] + 4] = 0x90;
                    m_vFileToPatch[patches[3] + 5] = 0x90;
                    m_vFileToPatch[patches[3] + 6] = 0xC7;
                    m_vFileToPatch[patches[3] + 9] = 0x00;
                    m_vFileToPatch[patches[3] + 10] = 0x00;
                    m_vFileToPatch[patches[3] + 11] = 0x00;
                    m_vFileToPatch[patches[3] + 12] = 0x00;
                    m_vFileToPatch[patches[3] + 13] = 0x90;
                    m_vFileToPatch[patches[3] + 14] = 0x90;
                    m_vFileToPatch[patches[3] + 15] = 0x90;
                    m_vFileToPatch[patches[3] + 16] = 0x90;
                    m_vFileToPatch[patches[3] + 17] = 0x90;
                    m_vFileToPatch[patches[3] + 18] = 0x90;
                    m_vFileToPatch[patches[3] + 19] = 0x90;
                    m_vFileToPatch[patches[3] + 20] = 0x90;
                    m_vFileToPatch[patches[3] + 21] = 0x90;
                    m_vFileToPatch[patches[3] + 22] = 0x90;
                    m_vFileToPatch[patches[3] + 23] = 0x90;
                    m_vFileToPatch[patches[3] + 24] = 0x90;
                }
                else if (patchPerms[3] == 1)
                {
                    m_vFileToPatch[patches[3]] = 0x90;
                    m_vFileToPatch[patches[3] + 1] = 0x90;
                    m_vFileToPatch[patches[3] + 2] = 0x90;
                    m_vFileToPatch[patches[3] + 3] = 0xC7;
                    m_vFileToPatch[patches[3] + 4] = 0x45;
                    m_vFileToPatch[patches[3] + 6] = 0x00;
                    m_vFileToPatch[patches[3] + 7] = 0x00;
                    m_vFileToPatch[patches[3] + 8] = 0x00;
                    m_vFileToPatch[patches[3] + 9] = 0x00;
                    m_vFileToPatch[patches[3] + 10] = 0x90;
                    m_vFileToPatch[patches[3] + 11] = 0x90;
                    m_vFileToPatch[patches[3] + 12] = 0x90;
                    m_vFileToPatch[patches[3] + 13] = 0x90;
                    m_vFileToPatch[patches[3] + 14] = 0x90;
                }
                else if (patchPerms[3] == 2)
                {
                    m_vFileToPatch[patches[3]] = 0x90;
                    m_vFileToPatch[patches[3] + 1] = 0x90;
                    m_vFileToPatch[patches[3] + 2] = 0x90;
                    m_vFileToPatch[patches[3] + 3] = 0xC7;
                    m_vFileToPatch[patches[3] + 4] = 0x84;
                    m_vFileToPatch[patches[3] + 7] = 0x00;
                    m_vFileToPatch[patches[3] + 8] = 0x00;
                    m_vFileToPatch[patches[3] + 9] = 0x00;
                    m_vFileToPatch[patches[3] + 10] = 0x00;
                    m_vFileToPatch[patches[3] + 11] = 0x00;
                    m_vFileToPatch[patches[3] + 12] = 0x00;
                    m_vFileToPatch[patches[3] + 13] = 0x00;
                    m_vFileToPatch[patches[3] + 14] = 0x90;
                    m_vFileToPatch[patches[3] + 15] = 0x90;
                    m_vFileToPatch[patches[3] + 16] = 0x90;
                    m_vFileToPatch[patches[3] + 17] = 0x90;
                    m_vFileToPatch[patches[3] + 18] = 0x90;
                }
                else if (patchPerms[3] == 3) //337.50+
                {
                    m_vFileToPatch[patches[3]] = 0x90;
                    m_vFileToPatch[patches[3] + 1] = 0x90;
                    m_vFileToPatch[patches[3] + 2] = 0x90;
                    m_vFileToPatch[patches[3] + 3] = 0x90;
                    m_vFileToPatch[patches[3] + 4] = 0x90;
                    m_vFileToPatch[patches[3] + 5] = 0x90;
                    m_vFileToPatch[patches[3] + 6] = 0xC7;
                    m_vFileToPatch[patches[3] + 7] = 0x45;
                    m_vFileToPatch[patches[3] + 9] = 0x00;
                    m_vFileToPatch[patches[3] + 10] = 0x00;
                    m_vFileToPatch[patches[3] + 11] = 0x00;
                    m_vFileToPatch[patches[3] + 12] = 0x00;
                    m_vFileToPatch[patches[3] + 13] = 0x90;
                    m_vFileToPatch[patches[3] + 14] = 0x90;
                    m_vFileToPatch[patches[3] + 15] = 0x90;
                    m_vFileToPatch[patches[3] + 16] = 0x90;
                    m_vFileToPatch[patches[3] + 17] = 0x90;
                    m_vFileToPatch[patches[3] + 18] = 0x90;
                    m_vFileToPatch[patches[3] + 19] = 0x90;
                    m_vFileToPatch[patches[3] + 20] = 0x90;
                    m_vFileToPatch[patches[3] + 21] = 0x90;
                    m_vFileToPatch[patches[3] + 22] = 0x90;
                    m_vFileToPatch[patches[3] + 23] = 0x90;
                    m_vFileToPatch[patches[3] + 24] = 0x90;
                }
                else if (patchPerms[3] == 4) //307.32
                {
                    m_vFileToPatch[patches[3]] = 0x90;
                    m_vFileToPatch[patches[3] + 1] = 0x90;
                    m_vFileToPatch[patches[3] + 2] = 0x90;
                    m_vFileToPatch[patches[3] + 3] = 0xC7;
                    m_vFileToPatch[patches[3] + 4] = 0x45;
                    m_vFileToPatch[patches[3] + 6] = 0x00;
                    m_vFileToPatch[patches[3] + 7] = 0x00;
                    m_vFileToPatch[patches[3] + 8] = 0x00;
                    m_vFileToPatch[patches[3] + 9] = 0x00;
                    m_vFileToPatch[patches[3] + 10] = 0x90;
                    m_vFileToPatch[patches[3] + 11] = 0x90;
                    m_vFileToPatch[patches[3] + 12] = 0x90;
                    m_vFileToPatch[patches[3] + 13] = 0x90;
                    m_vFileToPatch[patches[3] + 14] = 0x90;
                }

                for (int x = 4; x <= 9; x++)
                {
                    if (patchPerms[x] == 0)
                    {
                        m_vFileToPatch[patches[x]] = 0x90;
                        m_vFileToPatch[patches[x] + 1] = 0x90;
                        m_vFileToPatch[patches[x] + 2] = 0x90;
                        m_vFileToPatch[patches[x] + 3] = 0x90;
                    }
                    else if (patchPerms[x] == 1)
                    {
                        m_vFileToPatch[patches[x]] = 0x90;
                        m_vFileToPatch[patches[x] + 1] = 0x90;
                        m_vFileToPatch[patches[x] + 2] = 0x90;
                        m_vFileToPatch[patches[x] + 3] = 0x90;
                        m_vFileToPatch[patches[x] + 4] = 0x90;
                    }
                    else if (patchPerms[x] == 0)
                    {
                        m_vFileToPatch[patches[x]] = 0x90;
                        m_vFileToPatch[patches[x] + 1] = 0x90;
                        m_vFileToPatch[patches[x] + 2] = 0x90;
                        m_vFileToPatch[patches[x] + 3] = 0x90;
                        m_vFileToPatch[patches[x] + 4] = 0x90;
                        m_vFileToPatch[patches[x] + 5] = 0x90;
                        m_vFileToPatch[patches[x] + 6] = 0x90;
                        m_vFileToPatch[patches[x] + 7] = 0x90;
                        m_vFileToPatch[patches[x] + 8] = 0x90;
                    }
                }

                File.Move(m_DriverFile, string.Concat(m_DriverFile, ".bak"));
                using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(m_DriverFile)))
                {
                    bw.Write(m_vFileToPatch, 0, m_vFileToPatch.Length);
                    bw.Flush();
                    bw.Close();
                }
                listBoxLog.Log(ListBoxLog.Level.Success, string.Format("Patching was successful!"));
            }
            else
            {
                listBoxLog.Log(ListBoxLog.Level.Critical, string.Format("Patching was aborted because some patch locations could not be found."));
            }

            btnPatch.Enabled = true;
        }

        #region "sigScan Class Private Methods"
        /// <summary>
        /// MaskCheck
        /// 
        ///     Compares the current pattern byte to the current memory dump
        ///     byte to check for a match. Uses wildcards to skip bytes that
        ///     are deemed unneeded in the compares.
        /// </summary>
        /// <param name="nOffset">Offset in the dump to start at.</param>
        /// <param name="btPattern">Pattern to scan for.</param>
        /// <param name="strMask">Mask to compare against.</param>
        /// <returns>Boolean depending on if the pattern was found.</returns>
        private bool MaskCheck(int nOffset, byte[] btPattern, string strMask)
        {
            // Loop the pattern and compare to the mask and dump.
            for (int x = 0; x < btPattern.Length; x++)
            {
                // If the mask char is a wildcard, just continue.
                if (strMask[x] == '?')
                    continue;

                // If the mask char is not a wildcard, ensure a match is made in the pattern.
                if ((strMask[x] == 'x') && (btPattern[x] != this.m_vFileToPatch[nOffset + x]))
                    return false;
            }

            // The loop was successful so we found the pattern.
            return true;
        }

        /// <summary>
        /// FindPattern
        /// 
        ///     Attempts to locate the given pattern inside the dumped memory region
        ///     compared against the given mask. If the pattern is found, the offset
        ///     is added to the located address and returned to the user.
        /// </summary>
        /// <param name="btPattern">Byte pattern to look for in the dumped region.</param>
        /// <param name="strMask">The mask string to compare against.</param>
        /// <param name="startAddress">The offset to start searching.</param>
        /// <returns>-1 if not found, address if found, and -2 on retard.</returns>
        private int FindPattern(byte[] btPattern, string strMask, int startAddress)
        {
            try
            {
                // Read the file to patch into memory if we have not read it yet.
                if (this.m_vFileToPatch == null || this.m_vFileToPatch.Length == 0)
                {
                    this.m_vFileToPatch = File.ReadAllBytes(m_DriverFile);
                }

                if (startAddress < 0) return -1;

                // Ensure the mask and pattern lengths match.
                if (strMask.Length != btPattern.Length)
                    return -2;

                // Loop the region and look for the pattern.
                for (int x = startAddress; x < this.m_vFileToPatch.Length; x++)
                {
                    if (this.MaskCheck(x, btPattern, strMask))
                    {
                        // The pattern was found, return it.
                        return x;
                    }
                }

                // Pattern was not found.
                return -1;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        #endregion
    }
}
