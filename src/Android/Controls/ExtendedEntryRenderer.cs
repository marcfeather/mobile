﻿using System;
using System.ComponentModel;
using Android.Graphics;
using Android.Text;
using Android.Text.Method;
using Android.Views.InputMethods;
using Android.Widget;
using Bit.Android.Controls;
using Bit.App.Controls;
using Bit.App.Enums;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

[assembly: ExportRenderer(typeof(ExtendedEntry), typeof(ExtendedEntryRenderer))]
namespace Bit.Android.Controls
{
    public class ExtendedEntryRenderer : EntryRenderer
    {
        private bool _isPassword;
        private bool _toggledPassword;

        protected override void OnElementChanged(ElementChangedEventArgs<Entry> e)
        {
            base.OnElementChanged(e);

            var view = (ExtendedEntry)Element;
            _isPassword = view.IsPassword;

            if(Control != null)
            {
                Control.SetIncludeFontPadding(false);
                if(e.NewElement != null && e.NewElement.IsPassword)
                {
                    Control.SetTypeface(Typeface.Default, TypefaceStyle.Normal);
                    Control.TransformationMethod = new PasswordTransformationMethod();
                }
            }

            SetBorder(view);
            SetMaxLength(view);
            SetReturnType(view);

            // Editor Action is called when the return button is pressed
            Control.EditorAction += (object sender, TextView.EditorActionEventArgs args) =>
            {
                if(view.ReturnType != ReturnType.Next)
                {
                    view.Unfocus();
                }

                // Call all the methods attached to base_entry event handler Completed
                view.InvokeCompleted();
            };

            if(view.DisableAutocapitalize)
            {
                Control.SetRawInputType(Control.InputType |= InputTypes.TextVariationEmailAddress);
            }

            if(view.Autocorrect.HasValue)
            {
                Control.SetRawInputType(Control.InputType |= InputTypes.TextFlagNoSuggestions);
            }

            view.ToggleIsPassword += (object sender, EventArgs args) =>
            {
                var cursorStart = Control.SelectionStart;
                var cursorEnd = Control.SelectionEnd;

                Control.TransformationMethod = _isPassword ? null : new PasswordTransformationMethod();

                // set focus
                Control.RequestFocus();

                if(_toggledPassword)
                {
                    // restore cursor position
                    Control.SetSelection(cursorStart, cursorEnd);
                }
                else
                {
                    // set cursor to end
                    Control.SetSelection(Control.Text.Length);
                }

                // show keyboard
                var app = XLabs.Ioc.Resolver.Resolve<global::Android.App.Application>();
                var inputMethodManager =
                    app.GetSystemService(global::Android.Content.Context.InputMethodService) as InputMethodManager;
                inputMethodManager.ShowSoftInput(Control, ShowFlags.Forced);
                inputMethodManager.ToggleSoftInput(ShowFlags.Forced, HideSoftInputFlags.ImplicitOnly);

                _isPassword = view.IsPasswordFromToggled = !_isPassword;
                _toggledPassword = true;
            };

            if(view.FontFamily == "monospace")
            {
                Control.Typeface = Typeface.Monospace;
            }
        }
        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var view = (ExtendedEntry)Element;

            if(e.PropertyName == ExtendedEntry.HasBorderProperty.PropertyName
                || e.PropertyName == ExtendedEntry.HasOnlyBottomBorderProperty.PropertyName
                || e.PropertyName == ExtendedEntry.BottomBorderColorProperty.PropertyName)
            {
                SetBorder(view);
            }
            else
            {
                base.OnElementPropertyChanged(sender, e);
                if(e.PropertyName == VisualElement.BackgroundColorProperty.PropertyName)
                {
                    Control.SetBackgroundColor(view.BackgroundColor.ToAndroid());
                }
            }

            if(view.FontFamily == "monospace")
            {
                Control.Typeface = Typeface.Monospace;
            }
        }

        private void SetReturnType(ExtendedEntry view)
        {
            if(view.ReturnType.HasValue)
            {
                switch(view.ReturnType.Value)
                {
                    case ReturnType.Go:
                        Control.ImeOptions = ImeAction.Go;
                        Control.SetImeActionLabel("Go", ImeAction.Go);
                        break;
                    case ReturnType.Next:
                        Control.ImeOptions = ImeAction.Next;
                        Control.SetImeActionLabel("Next", ImeAction.Next);
                        break;
                    case ReturnType.Search:
                        Control.ImeOptions = ImeAction.Search;
                        Control.SetImeActionLabel("Search", ImeAction.Search);
                        break;
                    case ReturnType.Send:
                        Control.ImeOptions = ImeAction.Send;
                        Control.SetImeActionLabel("Send", ImeAction.Send);
                        break;
                    default:
                        Control.SetImeActionLabel("Done", ImeAction.Done);
                        break;
                }
            }
        }

        private void SetBorder(ExtendedEntry view)
        {
            if(!view.HasBorder)
            {
                Control.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
            }
            else
            {
                Control.SetBackgroundColor(view.BottomBorderColor.ToAndroid());
            }
        }

        private void SetMaxLength(ExtendedEntry view)
        {
            Control.SetFilters(new IInputFilter[] { new InputFilterLengthFilter(view.MaxLength) });
        }
    }
}
