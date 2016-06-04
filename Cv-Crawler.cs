using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using Newtonsoft.Json;
using System.Data.SqlClient;
using System.IO;
using System.Configuration;
using System.Data;
using HtmlAgilityPack;
using System.Threading;
using LitJson;

namespace LinkedIn_for_sqlserver
{
    class Cv_Crawler
    {
        void getuser(string name, List<string> qa, DataTable dt_sa, DataRow r)
        {
            int flag = 0;
            string stacklink = "null";
            Dictionary<string, int> dic = new Dictionary<string, int>();
            dic.Clear();
            for (int i = 0; i < qa.Count; i++)
            {
                string url = qa[i].ToString();
                var uri = new Uri(url.ToString());
                var browser1 = new ScrapingBrowser();
                browser1.UserAgent = FakeUserAgents.Chrome;
                var html1 = browser1.DownloadString(uri);
                var htmlDocument = new HtmlAgilityPack.HtmlDocument();
                htmlDocument.LoadHtml(html1);
                var html = htmlDocument.DocumentNode;
                try
                {
                    var resall = html.CssSelect(".user-info");
                    foreach (var htmlnode in resall)
                    {
                        //string te = htmlnode.InnerText;
                        //if (te.IndexOf(name) == -1) continue;
                        var temp = htmlnode.SelectSingleNode("./div[2]");
                        if (temp.InnerHtml.IndexOf("<a href=") != -1)
                        {
                            temp = temp.SelectSingleNode("./a[1]");
                            string tmpurl = temp.Attributes["href"].Value.ToString();
                            stacklink = "http://stackoverflow.com/" + tmpurl;
                            if (dic.ContainsKey(stacklink))
                            {
                                dic[stacklink] = dic[stacklink] + 1;
                            }
                            else
                            {
                                dic[stacklink] = 1;
                            }
                        }
                        
                        //if (tmpurl.StartsWith("/user"))
                        //{
                        //    flag = 1;
                        //    stacklink = "http://stackoverflow.com/" + tmpurl;
                        //    break;
                        //}
                    }
                    //if (flag == 1) break;
                }
                catch (Exception e)
                {
                    throw;
                }

            }
            stacklink = "null";
            int maxx = 0;
            foreach(var item in dic)
            {
                if(item.Value > maxx)
                {
                    stacklink = item.Key;
                    maxx = item.Value;
                }
            }

            if (stacklink == "null") return;
            if(stacklink.IndexOf("user") != -1)
            {
                int id = Convert.ToInt32(stacklink.Split(new[] { "users/" }, StringSplitOptions.None)[1].Split('/')[0]);
                r["SOFID"] = id;
            }
            r["Stackoverflow"] = stacklink;
        }
        void getanslink(string name, string url, DataTable dt_sa, DataRow r)
        {
            List<String> qa = new List<String>();
            qa.Clear();
            var uri = new Uri(url.ToString());
            var browser1 = new ScrapingBrowser();
            browser1.UserAgent = FakeUserAgents.Chrome;
            var html1 = browser1.DownloadString(uri);
            var htmlDocument = new HtmlAgilityPack.HtmlDocument();
            htmlDocument.LoadHtml(html1);
            var html = htmlDocument.DocumentNode;
            JsonData data = JsonMapper.ToObject(html1);
            for (int i = 0; i < data.Count; i++)
            {
                qa.Add(data[i]["AnswerLink"].ToString());
            }
            if (qa.Count() != 0)
            {
                string json_data = JsonConvert.SerializeObject(qa);
                r["Answers"] = json_data.ToString();
                getuser(name, qa, dt_sa, r);
            }

        }
        void work(int id, string name, string url, DataTable dt_sa, DataRow r)
        {
            var uri = new Uri(url.ToString());
            var browser1 = new ScrapingBrowser();
            browser1.UserAgent = FakeUserAgents.Chrome;
            var html1 = browser1.DownloadString(uri);
            var htmlDocument = new HtmlAgilityPack.HtmlDocument();
            htmlDocument.LoadHtml(html1);
            var html = htmlDocument.DocumentNode;
            HtmlNodeCollection hrefList = html.SelectNodes(".//a[@href]");
            List<String> other = new List<String>();
            other.Clear();

            if (hrefList != null)
            {
                foreach (HtmlNode href in hrefList)
                {
                    HtmlAttribute att = href.Attributes["href"];
                    string temp = att.Value.ToString();
                    if (temp.StartsWith("https") || temp.StartsWith("http"))
                    {
                        if (temp.IndexOf("//twitter.") != -1)
                        {
                            r["Twitter"] = temp;
                        }
                        else if (temp.IndexOf("//github.") != -1)
                        {
                            r["GitHub"] = temp;
                        }
                        else if (temp.IndexOf("linkedin") != -1)
                        {
                            r["Linkedin"] = temp;
                        }
                        else
                        {
                            other.Add(temp);
                        }
                    }
                }
                string json_data = JsonConvert.SerializeObject(other);
                r["Others"] = json_data.ToString();
                r["flag"] = 0;
                var usepagelink = html.CssSelect(".print-row");
                string answerurl = "https://stackoverflow.com/jobs/cv/stack/answers/";
                foreach (var htmlnode in usepagelink)
                {
                    string temp = htmlnode.InnerHtml.ToString();
                    temp = temp.Split(new[] { "href=\"/jobs/cv/" }, StringSplitOptions.None)[1].Split('/')[0];
                    answerurl += temp;
                }

                getanslink(name, answerurl, dt_sa, r);
                try
                {
                    dt_sa.Rows.Add(r);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

            }
        }

        public  void initset(DataTable dt_sa)
        {
            DataColumn workCol;
            workCol = dt_sa.Columns.Add("SOFID", typeof(Int32));
            dt_sa.Columns.Add("Name", typeof(string));
            dt_sa.Columns.Add("Twitter", typeof(string));
            dt_sa.Columns.Add("GitHub", typeof(string));
            dt_sa.Columns.Add("Linkedin", typeof(string));
            dt_sa.Columns.Add("Stackoverflow", typeof(string));
            dt_sa.Columns.Add("Answers", typeof(string));
            dt_sa.Columns.Add("Others", typeof(string));
            workCol = dt_sa.Columns.Add("Url", typeof(string));
            workCol.Unique = true;
            dt_sa.Columns.Add("flag", typeof(int));
        }
        public void CVCrawler()
        {
            SqlServerOperation SqlOperation = new SqlServerOperation();
            while (true)
            {
                DataTable dt_sa = new DataTable();
                initset(dt_sa);
                string query = "select top 50 * from careers where flag = 0";
                DataTable dt = new DataTable();
                dt = SqlServerOperation.GetDataTable(query);
                int idbegin = -1, id = 1;
                foreach (DataRow row in dt.Rows)
                {
                    if (idbegin == -1)
                    {
                        idbegin = Convert.ToInt32(row[0]);
                    }
                    id = Convert.ToInt32(row[0]);
                    string name = row[1].ToString();
                    name = name.Trim();
                    string link = row[3].ToString();
                    link = link.Trim();
                    try
                    {
                        DataRow r = dt_sa.NewRow();
                        r["SOFID"] = -1;
                        r["Name"] = name;
                        r["Url"] = link;
                        Console.WriteLine(id + name);

                        work(id, name, link, dt_sa, r);

                    }
                    catch (Exception e)
                    {
                        Output write = new Output();
                        write.writetofile(e);
                        //throw;
                    }
                }
                SqlServerOperation.ExecuteSqlBulkCopy(dt_sa, "dbo.social_account");
                string temp2 = "update careers set flag  = 1 where SOFID <= " + id + " and SOFID >= " + idbegin;
                SqlServerOperation.ExecuteNonQuery(temp2);
            }

        }
    }
}
