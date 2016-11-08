﻿using Bit.App.Abstractions;
using System;
using Xamarin.Forms;
using XLabs.Ioc;

namespace Bit.App.Controls
{
    public class FormEntryCell : ExtendedViewCell
    {
        public FormEntryCell(
            string labelText,
            Keyboard entryKeyboard = null,
            bool IsPassword = false,
            VisualElement nextElement = null,
            bool useLabelAsPlaceholder = false,
            string imageSource = null,
            Thickness? containerPadding = null,
            bool useButton = false)
        {
            if(!useLabelAsPlaceholder)
            {
                Label = new Label
                {
                    Text = labelText,
                    FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Label)),
                    Style = (Style)Application.Current.Resources["text-muted"],
                    HorizontalOptions = LayoutOptions.FillAndExpand
                };
            }

            Entry = new ExtendedEntry
            {
                Keyboard = entryKeyboard,
                HasBorder = false,
                IsPassword = IsPassword,
                AllowClear = true,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                FontSize = Device.GetNamedSize(NamedSize.Medium, typeof(Entry))
            };

            if(useLabelAsPlaceholder)
            {
                Entry.Placeholder = labelText;
            }

            if(nextElement != null)
            {
                Entry.ReturnType = Enums.ReturnType.Next;
                Entry.Completed += (object sender, EventArgs e) => { nextElement.Focus(); };
            }

            var imageStackLayout = new StackLayout
            {
                Padding = containerPadding ?? new Thickness(15, 10),
                Orientation = StackOrientation.Horizontal,
                Spacing = 10,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.FillAndExpand
            };

            if(imageSource != null)
            {
                var tgr = new TapGestureRecognizer();
                tgr.Tapped += Tgr_Tapped;

                var theImage = new Image
                {
                    Source = imageSource,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Center
                };
                theImage.GestureRecognizers.Add(tgr);

                imageStackLayout.Children.Add(theImage);
            }

            var formStackLayout = new StackLayout
            {
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.CenterAndExpand
            };

            if(Device.OS == TargetPlatform.Android)
            {
                var deviceInfo = Resolver.Resolve<IDeviceInfoService>();
                if(useLabelAsPlaceholder)
                {
                    if(deviceInfo.Version < 21)
                    {
                        Entry.Margin = new Thickness(-9, 1, -9, 0);
                    }
                    else if(deviceInfo.Version == 21)
                    {
                        Entry.Margin = new Thickness(0, 4, 0, -4);
                    }
                }
                else
                {
                    Entry.AdjustMarginsForDevice();
                }

                if(containerPadding == null)
                {
                    imageStackLayout.AdjustPaddingForDevice();
                }
            }

            if(!useLabelAsPlaceholder)
            {
                formStackLayout.Children.Add(Label);
            }

            formStackLayout.Children.Add(Entry);
            imageStackLayout.Children.Add(formStackLayout);

            if(useButton)
            {
                Button = new ExtendedButton();
                imageStackLayout.Children.Add(Button);
            }

            Tapped += FormEntryCell_Tapped;

            View = imageStackLayout;
        }

        public Label Label { get; private set; }
        public ExtendedEntry Entry { get; private set; }
        public ExtendedButton Button { get; private set; }

        private void Tgr_Tapped(object sender, EventArgs e)
        {
            Entry.Focus();
        }

        private void FormEntryCell_Tapped(object sender, EventArgs e)
        {
            Entry.Focus();
        }
    }
}
