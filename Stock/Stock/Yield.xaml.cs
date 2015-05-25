﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Stock.Controller.DrawController;
using Stock.Controller.NetController;
using Stock.Controller.DBController.DBTable;

using Stock.UIController;

using System.Threading;

namespace Stock
{
    /// <summary>
    /// Yield.xaml 的交互逻辑
    /// </summary>
    public partial class Yield : Window
    {
        private List<string> idl;
        public Yield()
        {
            InitializeComponent();
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (e.GetPosition((IInputElement)sender).Y < title.Height.Value)
                {
                    this.DragMove();
                }
            }
        }

        private void Min_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Close_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DrawDataController DDC = new DrawDataController((int)(yield.Width), (int)(yield.Height));
            yield.Source = Adapter.ImageAdapter.ImageConvert(DDC.GetImage());
            List<StockHoldEntity> SHEL;
            DBSyncController.Handler().StockHoldReadAll(out SHEL);
            idl = SHEL.Select(s => s.id).ToList();
            NetState.IdConvert(ref idl);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DateTime date;
            int days;
            try
            {
                date = Convert.ToDateTime(StartDate.Text);
                days = Convert.ToInt32(DateLong.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("请输入正确的日期");
                return;
            }
            ThreadDate td = new ThreadDate();
            td.date = date;
            td.days = days;
            td.Width = (int)yield.Width;
            td.Height = (int)yield.Height;

//            ThreadImageGet(td);

            Thread get = new Thread(new ParameterizedThreadStart(ThreadImageGet));
            get.Start(td);
        }
        private void ThreadImageGet(object data)
        {
            ThreadDate td = (ThreadDate)data;
            List<HistoryStockHoldEntity> HSHELNoSort;
            DBSyncController.Handler().HistoryStockHoldReadByRange(td.date, td.days, out HSHELNoSort);
            Dictionary<string, double> dict = new Dictionary<string, double>();
            Dictionary<string, double> dict_ = new Dictionary<string, double>();
            foreach (HistoryStockHoldEntity HSHE in HSHELNoSort)
            {
                if (!dict.ContainsKey(HSHE.id))
                {
                    dict.Add(HSHE.id, 0);
                    dict_.Add(HSHE.id, 0);
                }
            }
            List<HistoryStockHoldEntity> HSHEL = HSHELNoSort.OrderBy(g => g.date).ToList();
            
            Dictionary<string,Dictionary<DateTime,double>> moneydict = new Dictionary<string,Dictionary<DateTime,double>>();

            foreach (string x in idl)
            {
                Dictionary<DateTime,double> money = new Dictionary<DateTime,double>();
                NetDataController.HistoryMoney(x, td.date, td.days, out money);
                moneydict.Add(x, money);
            }

            List<DrawDataEntity> DDEL = new List<DrawDataEntity>();
            DrawDataEntity DDE = new DrawDataEntity();
            int index = 0;
            double fixedmoney = MainWindow.principal;
            for (int i = 0; i < td.days; i++)
            {
                double money = 0;
                Dictionary<string, double> cdict = new Dictionary<string, double>(dict_);
                DateTime dt = td.date.AddDays(i);
                if (!moneydict.First().Value.ContainsKey(dt))
                {
                    foreach(var x in moneydict)
                    {
                        x.Value.Add(dt, x.Value[dt.AddDays(-1)]);
                    }
                }
                DDE.date = dt;
                if (dt > DateTime.Now)
                {
                    break;
                }
                while (index < HSHEL.Count - 1 && HSHEL[index].date <= dt)
                {
                    dict[HSHEL[index].id] += HSHEL[index].change;
                    cdict[HSHEL[index].id] += -HSHEL[index].change * HSHEL[index].money;
                    index++;
                }
                foreach (var x in dict)
                {
                    if (x.Value != 0)
                    {
                        string id = idl.Where(s => s.Substring(1) == x.Key).First();
                        if (id == null)
                            continue;
                        double m = moneydict[id][dt];
                        fixedmoney += cdict[x.Key];
                        money += m * x.Value;
                    }
                }
                money += fixedmoney;
                DDE.money = (money - MainWindow.principal) * 100 / MainWindow.principal;
                DDEL.Add(DDE);
            }
            DrawDataController DDC = new DrawDataController(td.Width, td.Height);
            DDC.DrawData(DDEL);
            Action<Image, System.Drawing.Bitmap> updateAction = new Action<Image, System.Drawing.Bitmap>(UpdateImage);
            yield.Dispatcher.BeginInvoke(updateAction, yield, DDC.GetImage());
        }
        private void UpdateImage(Image yield, System.Drawing.Bitmap bmp)
        {
            yield.Source = Adapter.ImageAdapter.ImageConvert(bmp);
        }
    }
    struct ThreadDate
    {
        public DateTime date;
        public int days;
        public int Width;
        public int Height;
    }
}
