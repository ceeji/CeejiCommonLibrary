using Ceeji.Data.BinaryPackage;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CPKEditor {
    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e) {

        }

        private void showPackageStructure(CPKPackage p) {
            treeView.Nodes.Clear();
            var root = treeView.Nodes.Add("Root [" + p.ContentType + ", " + p.Flags + "]");

            foreach (var node in p.Nodes)
                addNode(node.Value, root);

            root.Expand();

            labelFormat.Text = string.Format("CPK Format: {0}, Content Type: {1}", p.FormatVersion, p.ContentType);
        }

        private void addNode(CPKNode node, TreeNode treeParent) {
            var root = treeParent.Nodes.Add(node.Name + (node.SerializedLength < 40 ? " = " + node.Value.ToString() : "") +  string.Format(" [{0}, {1}]", node.Type, formatSize(node.SerializedLength)));
            root.Tag = node;

            if ((node.Type & CPKValueType.List) == CPKValueType.List) {
                foreach (var child in node.Value.Nodes)
                    addNode(child.Value, root);
            }
            if ((node.Type & CPKValueType.Array) == CPKValueType.Array) {
                for (var i = 0; i < node.Value.Items.Count; ++i) {
                    addValue(i, node.Value.Items[i], root);
                } 
            }

            root.Expand();
        }

        private void addValue(int index, CPKValue node, TreeNode treeParent) {
            var root = treeParent.Nodes.Add(index + " [" + node.Type.ToString() + "]");
            root.Tag = node;

            if ((node.Type & CPKValueType.List) == CPKValueType.List) {
                foreach (var child in node.Nodes)
                    addNode(child.Value, root);
            }
            if ((node.Type & CPKValueType.Array) == CPKValueType.Array) {
                for (var i = 0; i < node.Items.Count; ++i) {
                    addValue(i, node.Items[i], root);
                }
            }
        }

        private string formatSize(double size) {
            if (size < 1024)
                return (int)size + " Byte";
            else if (size < 1024 * 1024)
                return (int)(size / 1024d) + " KB";
            else
                return (int)(size / (1024 * 1024d)) + " MB";
        }

        private void button1_Click(object sender, EventArgs e) {
            try {
                if (this.openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    using (var file = File.OpenRead(openFileDialog1.FileName)) {
                        current = new CPKPackage(file);
                        showPackageStructure(current);
                    }
                }
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
        }

        private CPKPackage current;

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e) {
            if (e.Node != null && e.Node.Tag != null) {
                if (e.Node.Tag is CPKNode)
                    this.textBox1.Text = (e.Node.Tag as CPKNode).Value.ToString();
                else
                    this.textBox1.Text = (e.Node.Tag as CPKValue).Value.ToString();
            }
        }
    }
}
