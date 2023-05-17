using Avalonia.Svg.Skia;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.Views.Input;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class ControllerInputViewModel : BaseModel
    {
        private ControllerInputConfig _config;
        private bool _isLeft;
        private bool _isRight;
        private bool _showSettings;
        private SvgImage _image;

        public ControllerInputConfig Config
        {
            get => _config;
            set
            {
                _config = value;
                OnPropertyChanged();
            }
        }

        public bool IsLeft
        {
            get => _isLeft;
            set
            {
                _isLeft = value;
                OnPropertyChanged();
            }
        }

        public bool IsRight
        {
            get => _isRight;
            set
            {
                _isRight = value;
                OnPropertyChanged();
            }
        }

        public bool ShowSettings
        {
            get => _showSettings;
            set
            {
                _showSettings = value;
                OnPropertyChanged();
            }
        }

        public SvgImage Image
        {
            get => _image;
            set
            {
                _image = value;
                OnPropertyChanged();
            }
        }

        public async void ShowMotionConfig()
        {
            await MotionInputView.Show(this);
        }

        public async void ShowRumbleConfig()
        {
            await RumbleInputView.Show(this);
        }
    }
}