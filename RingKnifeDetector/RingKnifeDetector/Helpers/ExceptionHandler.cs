using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace RingKnifeDetector.Helpers
{
    /// <summary>
    /// 全局异常处理器
    /// </summary>
    public static class ExceptionHandler
    {
        /// <summary>
        /// 初始化全局异常处理
        /// </summary>
        public static void Initialize()
        {
            // 处理UI线程未捕获异常
            Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // 处理非UI线程未捕获异常
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // 处理Task未观察到的异常
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var errorMessage = $"应用程序发生错误:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}";
            
            MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // 标记异常已处理，防止应用程序崩溃
            e.Handled = true;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            var errorMessage = $"应用程序发生严重错误:\n\n{exception?.Message}\n\n{exception?.StackTrace}";
            
            MessageBox.Show(errorMessage, "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            var errorMessage = $"异步任务发生错误:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}";
            
            MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // 标记异常已观察
            e.SetObserved();
        }
    }
}