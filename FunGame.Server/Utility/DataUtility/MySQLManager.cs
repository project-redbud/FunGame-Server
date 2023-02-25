using System.Data;
using System.Text;
using Milimoe.FunGame.Core.Api.Data;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Service;

namespace Milimoe.FunGame.Server.Utility
{
    public class MySQLManager : SQLManager
    {
        public new SQLHelper SQLHelper { get; }

        public MySQLManager(SQLHelper SQLHelper)
        {
            this.SQLHelper = SQLHelper;
        }

        public override int Add(StringBuilder sql, ref SQLResult result)
        {
            return 0;
        }

        public override int Add(string sql, ref SQLResult result)
        {
            return 0;
        }

        public override SQLResult Execute()
        {
            return SQLResult.NotFound;
        }

        public override SQLResult Execute(StringBuilder sql)
        {
            return SQLResult.NotFound;
        }

        public override SQLResult Execute(string sql)
        {
            return SQLResult.NotFound;
        }

        public override DataSet ExecuteDataSet(StringBuilder sql)
        {
            return new DataSet();
        }

        public override DataSet ExecuteDataSet(string sql)
        {
            return new DataSet();
        }

        public override object Query(EntityType type, StringBuilder sql)
        {
            return General.EntityInstance;
        }

        public override object Query(EntityType type, string sql)
        {
            return General.EntityInstance;
        }

        public override int Remove(StringBuilder sql, ref SQLResult result)
        {
            return 0;
        }

        public override int Remove(string sql, ref SQLResult result)
        {
            return 0;
        }

        public override int Update(StringBuilder sql, ref SQLResult result)
        {
            return 0;
        }

        public override int Update(string sql, ref SQLResult result)
        {
            return 0;
        }
    }
}
