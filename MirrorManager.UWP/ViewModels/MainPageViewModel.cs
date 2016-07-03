using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MirrorManager.UWP.ViewModels
{
    public class MainPageViewModel : INotifyPropertyChanged
    {

        private string userName;

        public string UserName
        {
            get { return userName; }
            set { Set(ref userName, value); RaisePropertyChanged("Greeting"); }
        }

        public string Greeting => $"Hello, {UserName}";

        private bool oneFacePresent;

        public bool OneFacePresent
        {
            get { return oneFacePresent; }
            set { Set(ref oneFacePresent, value); }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        public void Set<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (!Equals(storage, value))
            {
                storage = value;
                RaisePropertyChanged(propertyName);
            }
        }

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null) =>
           PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion
    }
}
