using System;
using System.Collections.Generic;
using System.Security.Principal;

namespace PlanetaryProcessor
{
    /// <summary>
    /// Simple ConfigNode-alike implementation of a data storage
    /// </summary>
    public class NodeTree
    {
        private Dictionary<String, String> _values;

        private Dictionary<String, NodeTree> _nodes;

        public NodeTree()
        {
            _values = new Dictionary<String, String>();
            _nodes = new Dictionary<String, NodeTree>();
        }

        /// <summary>
        /// Grab the subnode that is assigned from the node
        /// </summary>
        public NodeTree GetNode(String name)
        {
            if (_nodes.ContainsKey(name))
            {
                return _nodes[name];
            }

            return null;
        }

        /// <summary>
        /// Add a subnode to the node
        /// </summary>
        public NodeTree AddNode(String name)
        {
            SetNode(name, new NodeTree());
            return GetNode(name);
        }

        /// <summary>
        /// Add a subnode to the node
        /// </summary>
        public void SetNode(String name, NodeTree node)
        {
            if (_nodes.ContainsKey(name))
            {
                _nodes[name] = node;
            }
            _nodes.Add(name, node);
        }

        /// <summary>
        /// Grab the value that is assigned to the key from the node
        /// </summary>
        public String GetValue(String name)
        {
            if (_values.ContainsKey(name))
            {
                return _values[name];
            }

            return null;
        }

        /// <summary>
        /// Assign a value to a key in the node
        /// </summary>
        public void SetValue(String name, String value)
        {
            if (_values.ContainsKey(name))
            {
                _values[name] = value;
            }
            _values.Add(name, value);
        }

        /// <summary>
        /// Convert a node tree into a string representation
        /// </summary>
        public override String ToString()
        {
            return ToString(0);
        }

        /// <summary>
        /// Convert a node tree into a string representation
        /// </summary>
        private String ToString(Int32 level)
        {
            // Create a string for the indentation level of this node
            String s = "";
            String indent = "";
            for (Int32 i = 0; i < level; i++)
            {
                indent += "    ";
            }
            
            // Add the values of the node to the string
            foreach (KeyValuePair<String, String> keyValuePair in _values)
            {
                s += indent + keyValuePair.Key + " = " + keyValuePair.Value + "\n";
            }

            // Add the nodes from the node to the string
            foreach (KeyValuePair<String, NodeTree> keyValuePair in _nodes)
            {
                s += indent + keyValuePair.Key + "\n";
                s += indent + "{\n";
                s += keyValuePair.Value.ToString(level + 1);
                s += indent + "}\n";
            }

            return s;
        }
    }
}