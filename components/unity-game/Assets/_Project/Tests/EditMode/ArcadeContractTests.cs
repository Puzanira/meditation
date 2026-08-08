using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Meditation.Tests
{
    /// <summary>
    /// ARCADE_INTEGRATION_CONTRACT §4, guarded locally: gameplay code reads input ONLY through
    /// ArcadeInput. If a keyboard hack ever creeps into a scenette, this fails before the cabinet
    /// integration finds out.
    /// </summary>
    public class ArcadeContractTests
    {
        private static readonly string[] Forbidden =
        {
            "UnityEngine.Input",
            "UnityEngine.InputSystem",
            "Input.GetKey",
            "Input.GetAxis",
            "Input.mousePosition",
            "Keyboard.current",
            "Mouse.current",
            "Gamepad.current"
        };

        /// <summary>Code only — prose about the contract must not trip the contract.</summary>
        private static string CodeOf(string file)
        {
            string text = File.ReadAllText(file);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            return System.Text.RegularExpressions.Regex.Replace(text, @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);
        }

        [Test]
        public void GameplayCode_ReadsInputOnlyThroughArcadeInput()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "Scripts");
            Assert.IsTrue(Directory.Exists(root), "Gameplay scripts must live in Assets/_Project/Scripts.");

            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string text = CodeOf(file);
                foreach (string needle in Forbidden)
                {
                    if (!text.Contains(needle)) continue;
                    offenders.Add(Path.GetFileName(file) + " → " + needle);
                }
            }

            Assert.IsEmpty(offenders,
                "Raw input in gameplay code (use ArcadeInput): " + string.Join(", ", offenders));
        }

        /// <summary>
        /// Done contract §5 of the sensors increment: the отгон is read from the cabinet's height
        /// sensors THROUGH the package facade, and from nowhere else.
        ///
        /// The scanner above cannot see this: reading a keyboard directly would be caught, but so
        /// would nothing at all — a detector fed a made-up number, or one still fed the joystick,
        /// passes it perfectly. So the two places that pump the loop (the game's flow and the stand's
        /// scenette skeleton) are named here, and each has to be reading both sensors off ArcadeInput.
        /// </summary>
        [Test]
        public void TheOtgon_IsReadFromTheHeightSensors_ThroughArcadeInput()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string[] pumps =
            {
                Path.Combine(root, "Game", "GameFlow.cs"),
                Path.Combine(root, "Stand", "PreviewSceneController.cs")
            };

            foreach (string file in pumps)
            {
                Assert.IsTrue(File.Exists(file), file + " — где-то тут игра качает ввод, файла нет.");
                string text = CodeOf(file);

                StringAssert.Contains("ArcadeInput.HeightA.Value", text,
                    Path.GetFileName(file) + ": отгон обязан читать датчик A через ArcadeInput.");
                StringAssert.Contains("ArcadeInput.HeightB.Value", text,
                    Path.GetFileName(file) + ": отгон обязан читать датчик B через ArcadeInput.");
            }
        }

        [Test]
        public void GameplayCode_DoesNotQuitTheApplication()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "Scripts");
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string text = CodeOf(file);
                Assert.IsFalse(text.Contains("Application.Quit("),
                    Path.GetFileName(file) + " must not own the process — the cabinet launcher does " +
                    "(ARCADE_INTEGRATION_CONTRACT §5).");
            }
        }
    }
}
