using System;
using System.Collections.Generic;
using AgenticAI.Core.ZeroCode.Models;

namespace AgenticAI.Core.ZeroCode.Recorder
{
    /// <summary>
    /// Action Normalizer - Normalizes DOM events into automation keywords
    /// Converts: click, type, select, pressEnter, submit, scroll, check, uncheck, etc.
    /// </summary>
    public class ActionNormalizer
    {
        private readonly Dictionary<string, Func<CapturedEvent, LocatorResult, NormalizedAction?>> _eventHandlers;

        public ActionNormalizer()
        {
            _eventHandlers = new Dictionary<string, Func<CapturedEvent, LocatorResult, NormalizedAction?>>
            {
                { "click", HandleClickEvent },
                { "dblclick", HandleDoubleClickEvent },
                { "input", HandleInputEvent },
                { "change", HandleChangeEvent },
                { "keydown", HandleKeydownEvent },
                { "submit", HandleSubmitEvent },
                { "scroll", HandleScrollEvent }
            };
        }

        /// <summary>
        /// Normalize a captured event into an automation action
        /// </summary>
        public NormalizedAction? Normalize(CapturedEvent capturedEvent, LocatorResult locator)
        {
            var eventType = capturedEvent.Type.ToLower();
            
            if (_eventHandlers.TryGetValue(eventType, out var handler))
            {
                return handler(capturedEvent, locator);
            }
            
            // Default: unknown action type
            return new NormalizedAction
            {
                ActionType = "Unknown",
                Locator = locator.Locator,
                Value = null,
                Description = $"Captured {eventType} event",
                Confidence = 0
            };
        }

        /// <summary>
        /// Handle click events -> "Click" action
        /// </summary>
        private NormalizedAction? HandleClickEvent(CapturedEvent evt, LocatorResult locator)
        {
            var tagName = evt.Target.TagName.ToUpper();
            
            // Special handling for links
            if (tagName == "A" && evt.Target.Attributes.ContainsKey("href"))
            {
                var href = evt.Target.Attributes["href"];
                return new NormalizedAction
                {
                    ActionType = "Click",
                    Locator = locator.Locator,
                    Value = null,
                    Description = $"Click link to '{href}'",
                    Confidence = locator.Confidence,
                    Metadata = new Dictionary<string, string>
                    {
                        { "href", href },
                        { "linkText", evt.Target.InnerText ?? "" }
                    }
                };
            }
            
            // Special handling for buttons
            if (tagName == "BUTTON" || (tagName == "INPUT" && evt.Target.Type == "button"))
            {
                return new NormalizedAction
                {
                    ActionType = "Click",
                    Locator = locator.Locator,
                    Value = null,
                    Description = $"Click button '{evt.Target.InnerText ?? evt.Target.Value}'",
                    Confidence = locator.Confidence
                };
            }
            
            // Special handling for checkboxes
            if (tagName == "INPUT" && evt.Target.Type == "checkbox")
            {
                return new NormalizedAction
                {
                    ActionType = evt.Target.Checked ? "Check" : "Uncheck",
                    Locator = locator.Locator,
                    Value = null,
                    Description = $"{(evt.Target.Checked ? "Check" : "Uncheck")} checkbox",
                    Confidence = locator.Confidence
                };
            }
            
            // Special handling for radio buttons
            if (tagName == "INPUT" && evt.Target.Type == "radio")
            {
                return new NormalizedAction
                {
                    ActionType = "Click",
                    Locator = locator.Locator,
                    Value = evt.Target.Value,
                    Description = $"Select radio button '{evt.Target.Value}'",
                    Confidence = locator.Confidence
                };
            }
            
            // Generic click
            return new NormalizedAction
            {
                ActionType = "Click",
                Locator = locator.Locator,
                Value = null,
                Description = $"Click on {tagName.ToLower()}",
                Confidence = locator.Confidence
            };
        }

        /// <summary>
        /// Handle double-click events -> "DoubleClick" action
        /// </summary>
        private NormalizedAction? HandleDoubleClickEvent(CapturedEvent evt, LocatorResult locator)
        {
            return new NormalizedAction
            {
                ActionType = "DoubleClick",
                Locator = locator.Locator,
                Value = null,
                Description = $"Double-click on {evt.Target.TagName.ToLower()}",
                Confidence = locator.Confidence
            };
        }

        /// <summary>
        /// Handle input events -> "Type" action
        /// </summary>
        private NormalizedAction? HandleInputEvent(CapturedEvent evt, LocatorResult locator)
        {
            var tagName = evt.Target.TagName.ToUpper();
            
            // Only process text inputs and textareas
            if ((tagName == "INPUT" && IsTextInputType(evt.Target.Type)) || tagName == "TEXTAREA")
            {
                return new NormalizedAction
                {
                    ActionType = "Type",
                    Locator = locator.Locator,
                    Value = evt.Target.Value,
                    Description = $"Type '{evt.Target.Value}' into {GetFieldDescription(evt)}",
                    Confidence = locator.Confidence,
                    Metadata = new Dictionary<string, string>
                    {
                        { "inputType", evt.Target.Type ?? "text" }
                    }
                };
            }
            
            return null; // Ignore non-text inputs
        }

        /// <summary>
        /// Handle change events -> "Select", "Check", "Uncheck" actions
        /// </summary>
        private NormalizedAction? HandleChangeEvent(CapturedEvent evt, LocatorResult locator)
        {
            var tagName = evt.Target.TagName.ToUpper();
            
            // Handle select dropdowns
            if (tagName == "SELECT")
            {
                return new NormalizedAction
                {
                    ActionType = "Select",
                    Locator = locator.Locator,
                    Value = evt.Target.Value,
                    Description = $"Select '{evt.Target.Value}' from dropdown",
                    Confidence = locator.Confidence,
                    Metadata = new Dictionary<string, string>
                    {
                        { "selectedIndex", evt.Target.SelectedIndex.ToString() }
                    }
                };
            }
            
            // Handle checkboxes
            if (tagName == "INPUT" && evt.Target.Type == "checkbox")
            {
                return new NormalizedAction
                {
                    ActionType = evt.Target.Checked ? "Check" : "Uncheck",
                    Locator = locator.Locator,
                    Value = evt.Target.Value,
                    Description = $"{(evt.Target.Checked ? "Check" : "Uncheck")} '{GetFieldDescription(evt)}'",
                    Confidence = locator.Confidence
                };
            }
            
            // Handle radio buttons
            if (tagName == "INPUT" && evt.Target.Type == "radio")
            {
                return new NormalizedAction
                {
                    ActionType = "Click",
                    Locator = locator.Locator,
                    Value = evt.Target.Value,
                    Description = $"Select radio option '{evt.Target.Value}'",
                    Confidence = locator.Confidence
                };
            }
            
            // Handle file inputs
            if (tagName == "INPUT" && evt.Target.Type == "file")
            {
                return new NormalizedAction
                {
                    ActionType = "UploadFile",
                    Locator = locator.Locator,
                    Value = evt.Target.Value,
                    Description = $"Upload file '{evt.Target.Value}'",
                    Confidence = locator.Confidence
                };
            }
            
            return null;
        }

        /// <summary>
        /// Handle keydown events -> "PressEnter", "PressEscape", "PressTab" actions
        /// </summary>
        private NormalizedAction? HandleKeydownEvent(CapturedEvent evt, LocatorResult locator)
        {
            var key = evt.Event.Key;
            
            if (string.IsNullOrWhiteSpace(key))
                return null;
            
            // Special keys that should be captured
            var specialKeys = new Dictionary<string, string>
            {
                { "Enter", "PressEnter" },
                { "Escape", "PressEscape" },
                { "Tab", "PressTab" },
                { "F1", "PressF1" },
                { "F2", "PressF2" },
                { "F5", "PressF5" }
            };
            
            if (specialKeys.TryGetValue(key, out var actionType))
            {
                return new NormalizedAction
                {
                    ActionType = actionType,
                    Locator = locator.Locator,
                    Value = null,
                    Description = $"Press {key} key",
                    Confidence = locator.Confidence,
                    Metadata = new Dictionary<string, string>
                    {
                        { "key", key },
                        { "shiftKey", evt.Event.ShiftKey.ToString() },
                        { "ctrlKey", evt.Event.CtrlKey.ToString() },
                        { "altKey", evt.Event.AltKey.ToString() }
                    }
                };
            }
            
            return null; // Ignore other keys
        }

        /// <summary>
        /// Handle submit events -> "Submit" action
        /// </summary>
        private NormalizedAction? HandleSubmitEvent(CapturedEvent evt, LocatorResult locator)
        {
            return new NormalizedAction
            {
                ActionType = "Submit",
                Locator = locator.Locator,
                Value = null,
                Description = "Submit form",
                Confidence = locator.Confidence
            };
        }

        /// <summary>
        /// Handle scroll events -> "Scroll" action
        /// </summary>
        private NormalizedAction? HandleScrollEvent(CapturedEvent evt, LocatorResult locator)
        {
            var scrollX = evt.Event.ScrollX;
            var scrollY = evt.Event.ScrollY;
            
            // Only capture significant scrolls
            if (scrollY < 100)
                return null;
            
            return new NormalizedAction
            {
                ActionType = "Scroll",
                Locator = "window",
                Value = $"{scrollX},{scrollY}",
                Description = $"Scroll to position (x:{scrollX}, y:{scrollY})",
                Confidence = 80,
                Metadata = new Dictionary<string, string>
                {
                    { "scrollX", scrollX.ToString() },
                    { "scrollY", scrollY.ToString() }
                }
            };
        }

        /// <summary>
        /// Check if input type is a text-based input
        /// </summary>
        private bool IsTextInputType(string type)
        {
            var textTypes = new[] { "text", "email", "password", "search", "tel", "url", "number", "" };
            return Array.Exists(textTypes, t => t.Equals(type, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get a descriptive field name
        /// </summary>
        private string GetFieldDescription(CapturedEvent evt)
        {
            // Try to get a descriptive name from attributes
            if (evt.Target.Attributes.TryGetValue("placeholder", out var placeholder))
                return placeholder;
            
            if (evt.Target.Attributes.TryGetValue("aria-label", out var ariaLabel))
                return ariaLabel;
            
            if (evt.Target.Attributes.TryGetValue("name", out var name))
                return name;
            
            if (!string.IsNullOrWhiteSpace(evt.Target.Id))
                return evt.Target.Id;
            
            return evt.Target.TagName.ToLower();
        }

        /// <summary>
        /// Deduplicate actions (remove rapid duplicate events)
        /// </summary>
        public bool ShouldDeduplicate(NormalizedAction current, NormalizedAction? previous)
        {
            if (previous == null)
                return false;
            
            // Deduplicate rapid "Type" actions on the same element
            if (current.ActionType == "Type" && previous.ActionType == "Type")
            {
                if (current.Locator == previous.Locator)
                {
                    // If typing in the same field within a short time, it's a duplicate
                    return true;
                }
            }
            
            // Deduplicate rapid "Scroll" actions
            if (current.ActionType == "Scroll" && previous.ActionType == "Scroll")
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Merge duplicate actions (e.g., update value for Type actions)
        /// </summary>
        public NormalizedAction MergeActions(NormalizedAction current, NormalizedAction previous)
        {
            if (current.ActionType == "Type" && previous.ActionType == "Type")
            {
                // Update the value with the latest input
                previous.Value = current.Value;
                previous.Description = current.Description;
                return previous;
            }
            
            if (current.ActionType == "Scroll" && previous.ActionType == "Scroll")
            {
                // Update scroll position with the latest
                previous.Value = current.Value;
                previous.Description = current.Description;
                return previous;
            }
            
            return current;
        }
    }

    /// <summary>
    /// Represents a normalized automation action
    /// </summary>
    public class NormalizedAction
    {
        public string ActionType { get; set; } = "";
        public string Locator { get; set; } = "";
        public string? Value { get; set; }
        public string Description { get; set; } = "";
        public int Confidence { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }

        /// <summary>
        /// Convert to RecordedAction model
        /// </summary>
        public RecordedAction ToRecordedAction(int timestamp)
        {
            return new RecordedAction
            {
                ActionType = this.ActionType,
                Locator = this.Locator,
                Value = this.Value,
                Description = this.Description,
                Timestamp = timestamp
            };
        }
    }
}
