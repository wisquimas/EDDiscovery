﻿/*
 * Copyright © 2015 - 2017 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 * 
 * EDDiscovery is not affiliated with Frontier Developments plc.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using EMK.Cartography;
using System.IO;
using Newtonsoft.Json;
using System.Diagnostics;
using EDDiscovery.DB;
using EDDiscovery;
using System.Threading;
using EMK.LightGeometry;
using EDDiscovery.EDSM;
using System.Collections.Concurrent;

namespace EDDiscovery
{
    public partial class RouteControl : UserControl
    {
        internal TravelHistoryControl travelhistorycontrol1;
        private EDDiscoveryForm _discoveryForm;
        internal bool changesilence = false;
        private List<SystemClass> routeSystems;
        string lastsys = null;

        public List<SystemClass>  RouteSystems { get {return routeSystems;} }

        System.Windows.Forms.Timer fromupdatetimer;
        System.Windows.Forms.Timer toupdatetimer;

        public RouteControl()
        {
            InitializeComponent();
            button_Route.Enabled = false;
            cmd3DMap.Enabled = false;
			richTextBox_routeresult.TextBox.ReadOnly = true;

            for (int i = 0; i < RoutePlotter.metric_options.Length; i++)
                comboBoxRoutingMetric.Items.Add(RoutePlotter.metric_options[i]);

        }

        public void InitControl(EDDiscoveryForm discoveryForm)
        {
            _discoveryForm = discoveryForm;
            fromupdatetimer = new System.Windows.Forms.Timer();
            fromupdatetimer.Interval = 500;
            fromupdatetimer.Tick += FromUpdateTick;

            toupdatetimer = new System.Windows.Forms.Timer();
            toupdatetimer.Interval = 500;
            toupdatetimer.Tick += ToUpdateTick;

            textBox_From.SetAutoCompletor(EDDiscovery.DB.SystemClass.ReturnSystemListForAutoComplete);
            textBox_To.SetAutoCompletor(EDDiscovery.DB.SystemClass.ReturnSystemListForAutoComplete);
        }

        private Thread ThreadRoute;

        private RoutePlotter CreateRoutePlotter()
        {
            RoutePlotter p = new RoutePlotter();
            string maxrangetext = textBox_Range.Text;
            if (!float.TryParse(maxrangetext, out p.maxrange)) p.maxrange = 30;
            p.usingcoordsfrom = textBox_From.ReadOnly == true;
            p.usingcoordsto = textBox_To.ReadOnly == true;
            GetCoordsFrom(out p.coordsfrom);                      // will be valid for a system or a co-ords box
            GetCoordsTo(out p.coordsto);
            p.fromsys = textBox_From.Text;
            p.tosys = textBox_To.Text;
            p.routemethod = comboBoxRoutingMetric.SelectedIndex;

            if (p.usingcoordsfrom)
                p.fromsys = "START POINT";
            if (p.usingcoordsto)
                p.tosys = "END POINT";

            p.possiblejumps = (int)(Point3D.DistanceBetween(p.coordsfrom, p.coordsto) / p.maxrange);

            return p;
        }

        private void button_Route_Click_1(object sender, EventArgs e)
        {
            ToggleButtons(false);           // beware the tab order, this moves the focus onto the next control, which in this dialog can be not what we want.
            richTextBox_routeresult.Clear();
            RoutePlotter plotter = CreateRoutePlotter();

            if (plotter.possiblejumps > 100)
            {
                DialogResult res = EDDiscovery.Forms.MessageBoxTheme.Show(_discoveryForm, "This will result in a large number (" + plotter.possiblejumps.ToString("0") + ") of jumps" + Environment.NewLine + Environment.NewLine + "Confirm please", "Confirm you want to compute", MessageBoxButtons.YesNo);
                if (res != System.Windows.Forms.DialogResult.Yes)
                {
                    ToggleButtons(true);
                    return;
                }
            }

            ThreadRoute = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(RouteMain));
            ThreadRoute.Name = "Thread Route";
            ThreadRoute.Start(plotter);
        }

        private void RouteMain(object _plotter)
        {
            RoutePlotter p = (RoutePlotter)_plotter;

            routeSystems = p.RouteIterative(AppendText);

            this.BeginInvoke(new Action(() => ToggleButtons(true)));
        }

        private void ToggleButtons(bool state)
        {
            button_Route.Enabled = state;
            cmd3DMap.Enabled = state;
        }


        private void textBox_Range_KeyPress(object sender, KeyPressEventArgs e)
        {
            Tools.TextBox_Numeric_KeyPress(sender, e);
        }

        public void UpdateHistorySystem(string str)
        {
            lastsys = str;
        }

        private void AppendText(string msg)
        {
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    richTextBox_routeresult.AppendText(msg);
                });
            }
            catch
            {
            }
        }

        string SystemNameOnly(string s)             // removes @ at end.
        {
            int atpos = s.IndexOf('@');
            if (s != null && atpos != -1)
                s = s.Substring(0, atpos);
            s.Trim();
            return s;
        }

        public void SaveSettings()
        {
            //Console.WriteLine("Save {0} {1} {2}", textBox_From.Text, textBox_To.Text , Environment.StackTrace);
            SQLiteDBClass.PutSettingString("RouteFrom", textBox_From.Text);
            SQLiteDBClass.PutSettingString("RouteTo", textBox_To.Text);
            SQLiteDBClass.PutSettingString("RouteRange", textBox_Range.Text);
            SQLiteDBClass.PutSettingString("RouteFromX", textBox_FromX.Text);
            SQLiteDBClass.PutSettingString("RouteFromY", textBox_FromY.Text);
            SQLiteDBClass.PutSettingString("RouteFromZ", textBox_FromZ.Text);
            SQLiteDBClass.PutSettingString("RouteToX", textBox_ToX.Text);
            SQLiteDBClass.PutSettingString("RouteToY", textBox_ToY.Text);
            SQLiteDBClass.PutSettingString("RouteToZ", textBox_ToZ.Text);
            SQLiteDBClass.PutSettingBool("RouteFromState", textBox_From.ReadOnly);
            SQLiteDBClass.PutSettingBool("RouteToState", textBox_To.ReadOnly);
            SQLiteDBClass.PutSettingInt("RouteMetric", comboBoxRoutingMetric.SelectedIndex);
        }

        public void EnableRouteTab()
        {
            textBox_From.Text = SQLiteDBClass.GetSettingString("RouteFrom", "");
            textBox_To.Text = SQLiteDBClass.GetSettingString("RouteTo", "");
            //Console.WriteLine("Load {0} {1}", textBox_From.Text, textBox_To.Text);
            textBox_Range.Text = SQLiteDBClass.GetSettingString("RouteRange", "30");
            if (textBox_Range.Text == "")
                textBox_Range.Text = "30";
            textBox_FromX.Text = SQLiteDBClass.GetSettingString("RouteFromX", "");
            textBox_FromY.Text = SQLiteDBClass.GetSettingString("RouteFromY", "");
            textBox_FromZ.Text = SQLiteDBClass.GetSettingString("RouteFromZ", "");
            textBox_ToX.Text = SQLiteDBClass.GetSettingString("RouteToX", "");
            textBox_ToY.Text = SQLiteDBClass.GetSettingString("RouteToY", "");
            textBox_ToZ.Text = SQLiteDBClass.GetSettingString("RouteToZ", "");
            bool fromstate = SQLiteDBClass.GetSettingBool("RouteFromState", false);
            bool tostate = SQLiteDBClass.GetSettingBool("RouteToState", false);

            int metricvalue = SQLiteDBClass.GetSettingInt("RouteMetric", 0);
            comboBoxRoutingMetric.SelectedIndex = (metricvalue >= 0 && metricvalue < comboBoxRoutingMetric.Items.Count) ? metricvalue : SystemClass.metric_waypointdev2;

            SelectToMaster(tostate);
            UpdateTo(true);
            SelectFromMaster(fromstate);
            UpdateFrom(true);
            textBox_Range.ReadOnly = false;
            comboBoxRoutingMetric.Enabled = true;
            richTextBox_routeresult.Text = "In either the From or To box areas, either enter a system name in the upper text Box," + Environment.NewLine +
                                "or enter a set of galactic co-ordinates in the bottom three boxes (xyz)." + Environment.NewLine + Environment.NewLine +
                                "If you enter a system, its co-ordinates will be shown in the lower three boxes." + Environment.NewLine +
                                "If you enter galactic co-ordinates, the nearest system will be shown in the upper box." + Environment.NewLine +
                                "Select the jump distance and hit the route planning button to find a list of waypoints to traverse." + Environment.NewLine + Environment.NewLine +

                                "Select the routing method to try different routes from a combination of:" + Environment.NewLine +
                                "  Waypoint distance - how far the star is from the next target co-ordinate" + Environment.NewLine +
                                "  Deviation from the straight line path - how far is the star taking you away" + Environment.NewLine +
                                "  from a straight line journey to the destination" + Environment.NewLine +
                                "  Options exist to limit the deviation allowed." + Environment.NewLine +
                                "  A mix of Options exist to limit the deviation allowed and allow combinations of" + Environment.NewLine +
                                "  waypoint distance and deviation. Experiment to find the best path" + Environment.NewLine + Environment.NewLine
                ;

        }

        private void textBox_Clicked(object sender, EventArgs e)
        {
            ((System.Windows.Forms.TextBox)sender).Select(0, 1000); // clicking highlights everything
        }

        private bool GetCoordsFrom(out Point3D pos)
        {
            double x = 0, y = 0, z = 0;

            bool worked = System.Double.TryParse(textBox_FromX.Text, out x) &&
                            System.Double.TryParse(textBox_FromY.Text, out y) &&
                            System.Double.TryParse(textBox_FromZ.Text, out z);
            pos = new Point3D(x, y, z);
            return worked;
        }

        public bool GetCoordsTo(out Point3D pos)
        {
            double x = 0, y = 0, z = 0;

            bool worked = System.Double.TryParse(textBox_ToX.Text, out x) &&
                            System.Double.TryParse(textBox_ToY.Text, out y) &&
                            System.Double.TryParse(textBox_ToZ.Text, out z);
            pos = new Point3D(x, y, z);
            return worked;
        }

        private bool IsValid()                          // have we star names or co-ords ready to go
        {
            bool readytocalc = true;
            Point3D pos;

            if (textBox_From.ReadOnly == false)          // if enabled, we are doing star names
            {
                if (_discoveryForm.history.FindSystem(SystemNameOnly(textBox_From.Text), _discoveryForm.galacticMapping) == null)
                    readytocalc = false;
            }
            else // check co-ords
            {
                if (!GetCoordsFrom(out pos))
                    readytocalc = false;
            }
            if (textBox_To.ReadOnly == false)          // if enabled, we are doing star names
            {
                if (_discoveryForm.history.FindSystem(SystemNameOnly(textBox_To.Text), _discoveryForm.galacticMapping) == null)
                    readytocalc = false;
            }
            else // check co-ords
            {
                if (!GetCoordsTo(out pos))
                    readytocalc = false;
            }

            if (comboBoxRoutingMetric.SelectedIndex < 0)
                readytocalc = false;

            return readytocalc;
        }

        ///////////////////////////////////////////////////////////////////////////// From..

        private void SelectFromMaster( bool coords )
        {
            textBox_From.ReadOnly = coords;
            textBox_FromX.ReadOnly = !coords;
            textBox_FromY.ReadOnly = !coords;
            textBox_FromZ.ReadOnly = !coords;
        }


        private void UpdateFrom(bool updatename)
        {
            changesilence = true;

            if (textBox_From.ReadOnly == false)                // if entering system name..
            {
                ISystem ds1 = _discoveryForm.history.FindSystem(SystemNameOnly(textBox_From.Text), _discoveryForm.galacticMapping);

                if (ds1 != null)
                {
                    if (updatename)                          // can't fix it as you type.. so leave alone
                    {
                        if (ds1 is GalacticMapSystem)
                        {
                            GalacticMapSystem gms = (GalacticMapSystem)ds1;
                            textBox_From.Text = gms.GalMapObject.name;
                            textBox_FromName.Text = gms.name;
                        }
                        else
                        {
                            textBox_From.Text = ds1.name;
                            textBox_FromName.Text = ds1.name;
                        }
                    }

                    textBox_FromX.Text = ds1.x.ToString("0.00");
                    textBox_FromY.Text = ds1.y.ToString("0.00");
                    textBox_FromZ.Text = ds1.z.ToString("0.00");
                }
                else
                    textBox_FromX.Text = textBox_FromY.Text = textBox_FromZ.Text = "";
            }
            else
            {
                string res = "";
                Point3D curpos;
                if (GetCoordsFrom(out curpos))
                {
                    ISystem nearest = SystemClass.FindNearestSystem(curpos.X, curpos.Y, curpos.Z);

                    if (nearest != null)
                    {
                        double distance = Point3D.DistanceBetween(curpos, new Point3D(nearest.x, nearest.y, nearest.z));
                        if (distance < 0.1)
                            res = nearest.name;
                        else
                            res = nearest.name + " @ " + distance.ToString("0.00") + "ly";
                    }
                }

                textBox_From.Text = res;
                textBox_FromName.Text = res;
            }

            UpdateDistance();

            changesilence = false;
            button_Route.Enabled = IsValid();
        }

        private void UpdateDistance()
        {
            Point3D from, to;
            string dist = "";
            if (GetCoordsFrom(out from) && GetCoordsTo(out to))
            {
                dist = Point3D.DistanceBetween(from, to).ToString("0.00");
            }

            textBox_Distance.Text = dist;
        }

        private void textBox_From_Enter(object sender, EventArgs e)
        {
            SelectFromMaster(false);                              // enable system box
            fromupdatetimer.Stop();
            fromupdatetimer.Start();
//            Console.WriteLine("XYZE: Start at " + Environment.TickCount);
        }

        private void textBox_FromXYZ_Enter(object sender, EventArgs e)
        {
            SelectFromMaster(true);                       // coords master
            fromupdatetimer.Stop();
            fromupdatetimer.Start();
            ((System.Windows.Forms.TextBox)sender).Select(0, 1000); // entering selects everything
        }

        private void textBox_From_TextChanged(object sender, EventArgs e)
        {
            if ( !changesilence )
            {
                fromupdatetimer.Stop();
                fromupdatetimer.Start();
            }
        }

        void FromUpdateTick(object sender, EventArgs e)
        {
            fromupdatetimer.Stop();
            UpdateFrom(false);
        }

        private void textBox_XYZ_From_Changed(object sender, EventArgs e)
        {
            if (!changesilence)
            {
                fromupdatetimer.Stop();
                fromupdatetimer.Start();
            }
        }
        
        private void buttonExtTravelFrom_Click(object sender, EventArgs e)
        {
            if (lastsys != null)
            {
                SelectFromMaster(false);                              // enable system box
                textBox_From.Text = lastsys;
                UpdateFrom(false);
            }
        }

        private void buttonTargetFrom_Click(object sender, EventArgs e)
        {
            string name;
            double x, y, z;

            if (TargetClass.GetTargetPosition(out name, out x, out y, out z))
            {
                SelectFromMaster(false);                              // enable system box
                textBox_From.Text = TargetClass.GetNameWithoutPrefix(name);
                UpdateFrom(false);
            }
        }

        //////////////////////////////////////////////////////////////////////////////////// To..

        private void SelectToMaster(bool coords)
        {
            textBox_To.ReadOnly = coords;
            textBox_ToX.ReadOnly = !coords;
            textBox_ToY.ReadOnly = !coords;
            textBox_ToZ.ReadOnly = !coords;
        }

    
        private void UpdateTo(bool updatename)
        {
            changesilence = true;

            if (textBox_To.ReadOnly == false)                // if entering system name..
            {
                ISystem ds1 = _discoveryForm.history.FindSystem(SystemNameOnly(textBox_To.Text), _discoveryForm.galacticMapping);

                if (ds1 != null)
                {
                    if (updatename)                          // can't fix it as you type.. so leave alone
                    {
                        if (ds1 is GalacticMapSystem)
                        {
                            GalacticMapSystem gms = (GalacticMapSystem)ds1;
                            textBox_To.Text = gms.GalMapObject.name;
                            textBox_ToName.Text = gms.name;
                        }
                        else
                        {
                            textBox_To.Text = ds1.name;
                            textBox_ToName.Text = ds1.name;
                        }
                    }

                    textBox_ToX.Text = ds1.x.ToString("0.00");
                    textBox_ToY.Text = ds1.y.ToString("0.00");
                    textBox_ToZ.Text = ds1.z.ToString("0.00");
                }
                else
                    textBox_ToX.Text = textBox_ToY.Text = textBox_ToZ.Text = "";
            }
            else // Co-ords..
            {
                string res = "";
                Point3D curpos;
                if (GetCoordsTo(out curpos))
                {
                    ISystem nearest = SystemClass.FindNearestSystem(curpos.X, curpos.Y, curpos.Z);

                    if (nearest != null)
                    {
                        double distance = Point3D.DistanceBetween(curpos, new Point3D(nearest.x, nearest.y, nearest.z));
                        if (distance < 0.1)
                            res = nearest.name;
                        else
                            res = nearest.name + " @ " + distance.ToString("0.00") + "ly";
                    }
                }

                textBox_To.Text = res;
                textBox_ToName.Text = res;
            }

            UpdateDistance();

            changesilence = false;
            button_Route.Enabled = IsValid();
        }

        private void textBox_To_Enter(object sender, EventArgs e)   // To has been tabbed/clicked..
        {
            SelectToMaster(false);                              // enable system box
            toupdatetimer.Stop();
            toupdatetimer.Start();
        }

        private void textBox_ToXYZ_Enter(object sender, EventArgs e)
        {
            SelectToMaster(true);                       // coords master
            toupdatetimer.Stop();
            toupdatetimer.Start();
            ((System.Windows.Forms.TextBox)sender).Select(0, 1000); // clicking highlights everything
        }

        private void textBox_To_TextChanged(object sender, EventArgs e)
        {
            if (!changesilence)
            {
                toupdatetimer.Stop();
                toupdatetimer.Start();
            }
        }

        private void textBox_XYZ_To_Changed(object sender, EventArgs e)
        {
            if (!changesilence)
            {
                toupdatetimer.Stop();
                toupdatetimer.Start();
            }
        }

        void ToUpdateTick(object sender, EventArgs e)
        {
            toupdatetimer.Stop();
            UpdateTo(false);
        }

        private void comboBoxRoutingMetric_SelectedIndexChanged(object sender, EventArgs e)
        {
            button_Route.Enabled = IsValid();
        }

        private void cmd3DMap_Click(object sender, EventArgs e)
        {
            var map = _discoveryForm.Map;

            if (routeSystems != null && routeSystems.Any())
            {
                float dist;
                if (!float.TryParse(textBox_Distance.Text, out dist))       // in case text is crap
                    dist = 30;

                _discoveryForm.history.FillInPositionsFSDJumps();
                map.Prepare(routeSystems.First(), _discoveryForm.GetHomeSystem(), routeSystems.First(), 400 / dist , _discoveryForm.history.FilterByTravel);
                map.SetPlanned(routeSystems);
                map.Show();
            }
            else
            {
                EDDiscovery.Forms.MessageBoxTheme.Show("No route set up, retry", "No Route", MessageBoxButtons.OK);
                return;
            }
        }

        private void buttonExtTravelTo_Click(object sender, EventArgs e)
        {
            if (lastsys != null)
            {
                SelectToMaster(false);                              // enable system box
                textBox_To.Text = lastsys;
                UpdateTo(false);
            }
        }

        private void buttonTargetTo_Click(object sender, EventArgs e)
        {
            string name;
            double x, y, z;

            if (TargetClass.GetTargetPosition(out name, out x, out y, out z))
            {
                SelectToMaster(false);                              // enable system box
                textBox_To.Text = TargetClass.GetNameWithoutPrefix(name);
                UpdateTo(false);
            }
        }
    }

    public class RoutePlotter
    {
        public float maxrange;
        public bool usingcoordsfrom;
        public Point3D coordsfrom;
        public bool usingcoordsto;
        public Point3D coordsto;
        public string fromsys;
        public string tosys;
        public int routemethod;
        public int possiblejumps;

        // METRICs defined by systemclass GetSystemNearestTo function
        public static string[] metric_options = {
            "Nearest to Waypoint",
            "Minimum Deviation from Path",
            "Nearest to Waypoint with dev<=100ly",
            "Nearest to Waypoint with dev<=250ly",
            "Nearest to Waypoint with dev<=500ly",
            "Nearest to Waypoint + Deviation / 2"
        };

        public List<SystemClass> RouteIterative(Action<string> AppendText)
        {
            double traveldistance = Point3D.DistanceBetween(coordsfrom, coordsto);      // its based on a percentage of the traveldistance
            List<SystemClass> routeSystems = new List<SystemClass>();
            System.Diagnostics.Debug.WriteLine("From " + fromsys + " to  " + tosys);
            routeSystems.Add(new SystemClass(fromsys, coordsfrom.X, coordsfrom.Y, coordsfrom.Z));

            AppendText("Searching route from " + fromsys + " to " + tosys + " using " + metric_options[routemethod] + " metric" + Environment.NewLine);
            AppendText("Total distance: " + traveldistance.ToString("0.00") + " in " + maxrange.ToString("0.00") + "ly jumps" + Environment.NewLine);

            AppendText(Environment.NewLine);
            AppendText(string.Format("{0,-40}    Depart          @ {1,9:0.00},{2,8:0.00},{3,9:0.00}" + Environment.NewLine, fromsys, coordsfrom.X, coordsfrom.Y, coordsfrom.Z));

            Point3D curpos = coordsfrom;
            int jump = 1;
            double actualdistance = 0;
#if DEBUG
            //Console.WriteLine("-------------------------- BEGIN");
#endif
            do
            {
                double distancetogo = Point3D.DistanceBetween(coordsto, curpos);      // to go

                if (distancetogo <= maxrange)                                         // within distance, we can go directly
                    break;

                Point3D travelvector = new Point3D(coordsto.X - curpos.X, coordsto.Y - curpos.Y, coordsto.Z - curpos.Z); // vector to destination
                Point3D travelvectorperly = new Point3D(travelvector.X / distancetogo, travelvector.Y / distancetogo, travelvector.Z / distancetogo); // per ly travel vector

                Point3D nextpos = new Point3D(curpos.X + maxrange * travelvectorperly.X,
                                              curpos.Y + maxrange * travelvectorperly.Y,
                                              curpos.Z + maxrange * travelvectorperly.Z);   // where we would like to be..

#if DEBUG
                //Console.WriteLine("Curpos " + curpos.X + "," + curpos.Y + "," + curpos.Z);
                //Console.WriteLine(" next" + nextpos.X + "," + nextpos.Y + "," + nextpos.Z);
#endif
                SystemClass bestsystem = SystemClass.GetSystemNearestTo(curpos, nextpos, maxrange, maxrange - 0.5, routemethod);

                string sysname = "WAYPOINT";
                double deltafromwaypoint = 0;
                double deviation = 0;

                if (bestsystem != null)
                {
                    Point3D bestposition = new Point3D(bestsystem.x, bestsystem.y, bestsystem.z);
                    deltafromwaypoint = Point3D.DistanceBetween(bestposition, nextpos);     // how much in error
                    deviation = Point3D.DistanceBetween(curpos.InterceptPoint(nextpos, bestposition), bestposition);
                    nextpos = bestposition;
                    sysname = bestsystem.name;
                    routeSystems.Add(bestsystem);
                }

                AppendText(string.Format("{0,-40}{1,3} Dist:{2,8:0.00}ly @ {3,9:0.00},{4,8:0.00},{5,9:0.00} WPd:{6,8:0.00}ly Dev:{7,8:0.00}ly" + Environment.NewLine,
                            sysname, jump, Point3D.DistanceBetween(curpos, nextpos), nextpos.X, nextpos.Y, nextpos.Z, deltafromwaypoint, deviation));

                actualdistance += Point3D.DistanceBetween(curpos, nextpos);
                curpos = nextpos;
                jump++;

            } while (true);

            routeSystems.Add(new SystemClass(tosys, coordsto.X, coordsto.Y, coordsto.Z));
            actualdistance += Point3D.DistanceBetween(curpos, coordsto);
            AppendText(string.Format("{0,-40}{1,3} Dist:{2,8:0.00}ly @ {3,9:0.00},{4,8:0.00},{5,9:0.00}" + Environment.NewLine, tosys, jump, Point3D.DistanceBetween(curpos, coordsto), coordsto.X, coordsto.Y, coordsto.Z));
            AppendText(Environment.NewLine);
            AppendText(string.Format("Straight Line Distance {0,8:0.00}ly vs Travelled Distance {1,8:0.00}ly" + Environment.NewLine, traveldistance, actualdistance));

            return routeSystems;
        }
    }
}
