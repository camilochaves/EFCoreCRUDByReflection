using System.Dynamic;
using Newtonsoft.Json;

namespace Universal.Context;
public partial class UniversalContext
{
    private readonly DbContext _context;
    public UniversalContext(DbContext context) => this._context = context;
    public object Add<T>(object obj) where T : class
    {
        var convertedObj = (T)obj;
        _context.Add(convertedObj);
        return convertedObj;
    }

    public object Add(string dbSetName, object obj)
    {
        var dbSetType = DbSetUnderlyingType(dbSetName);
        if (dbSetType is null) throw new Exception("DbSet type cannot be null!");
        var convertedObj = Convert.ChangeType(obj, dbSetType);
        _context.Add(convertedObj);
        return convertedObj;
    }
    public async Task<object> AddAsync<T>(object obj) where T : class
    {
        var convertedObj = (T)obj;
        await _context.AddAsync(convertedObj);
        return convertedObj;
    }

    public async Task<object> AddAsync(string dbSetName, object obj)
    {
        var dbSetType = DbSetUnderlyingType(dbSetName);
        if (dbSetType is null) throw new Exception("DbSet type cannot be null!");
        var convertedObj = Convert.ChangeType(obj, dbSetType);
        await _context.AddAsync(convertedObj);
        return convertedObj;
    }
    public object Remove<T>(object obj) where T : class
    {
        var convertedObj = (T)obj;
        _context.Set<T>().Remove(convertedObj);
        return convertedObj;
    }

    public object Remove(string dbSetName, object obj)
    {
        var dbSetType = DbSetUnderlyingType(dbSetName);
        if (dbSetType is null) throw new Exception("DbSet type cannot be null!");
        var convertedObj = Convert.ChangeType(obj, dbSetType);
        _context.Remove(convertedObj);
        return convertedObj;
    }
    public IEnumerable<object> Remove<T>(Expression<Func<T, bool>> p) where T : class
    {
        try
        {
            var pred = p.Compile();
            var objs = _context.Set<T>().Where(p);
            _context.Set<T>().RemoveRange(objs);
            return objs;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public IEnumerable<object> Remove(string dbSetName, string where)
    {
        try
        {
            var objs = Query(dbSetName, where);
            var dbSetType = DbSetUnderlyingType(dbSetName);
            if (dbSetType is null) throw new Exception("dbSetType cannot be null!");
            List<object> convertedObjects = new();
            foreach (var obj in objs?.AsEnumerable<object>())
            {
                convertedObjects.Add(Convert.ChangeType(obj, dbSetType));
            }
            _context.RemoveRange(convertedObjects);
            return convertedObjects;
        }
        catch (Exception)
        {
            throw;
        }
    }
    public T? Get<T>(Expression<Func<T, bool>> p) where T : class
    {
        try
        {
            var pred = p.Compile();
            return _context.Set<T>().Where(p).AsEnumerable<T>().SingleOrDefault();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public object? Get(string dbSetName, string where)
    {
        try
        {
            var collection = Query(dbSetName, where);
            return collection?.SingleOrDefault();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public object? Find(Type entityType, params object?[] keys) => _context.Find(entityType, keys);
    public T Update<T>(object obj) where T : class
    {
        var convertedObj = (T)obj;
        _context.Set<T>().Update(convertedObj);
        return convertedObj;
    }

    private object Update(Type dbSetType, object target, object source, IEnumerable<string>? keyNames = null)
    {
        if (target.GetType() != dbSetType)
        {
            target = Convert.ChangeType(target, dbSetType);
            if (target is null) throw new Exception("Update error! Target cannot be null");
        }
        _context.Entry(target).State = EntityState.Modified;
        Copy(ref target, source, keyNames);
        return target;
    }

    private object Update(Type dbSetType, object target, ExpandoObject source, IEnumerable<string>? keyNames = null)
    {
        _context.Entry(target).State = EntityState.Modified;
        foreach (var item in source as IDictionary<string, object>)
        {
            if (keyNames?.Contains(item.Key) ?? false) continue;
            var targetProp = target.GetType().GetProperty(item.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.IgnoreCase);
            if (targetProp is null) throw new Exception("Keynames do not belong to target object");
            Type t = Nullable.GetUnderlyingType(targetProp!.PropertyType) ?? targetProp!.PropertyType;
            var convertedValue = Convert.ChangeType(item.Value, t);
            targetProp.SetValue(target, convertedValue);
        }
        return target;
    }

    public object Update(string dbSetName, object target, object source, IEnumerable<string>? keyNames = null)
    {
        var dbSetType = DbSetUnderlyingType(dbSetName);
        if (dbSetType is null) throw new Exception("dbSetType cannot be null!");
        return Update(dbSetType, target, source, keyNames);
    }

    public object Update(string dbSetName, object sourceWithId, IEnumerable<string>? keyNames = null)
    {
        try
        {
            var dbSetType = DbSetUnderlyingType(dbSetName);
            if (dbSetType is null) throw new Exception("dbSetType cannot be null!");
            var keys = GetKeysValues(sourceWithId, keyNames);
            var targetInDb = Find(dbSetType, keys?.ToArray()!)!;
            if (targetInDb is null) throw new Exception("Body object with provided Keys not found in Database");
            return Update(dbSetType, targetInDb, sourceWithId, keyNames);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public object Update(string dbSetName, ExpandoObject sourceWithId, IEnumerable<string>? keyNames = null)
    {
        try
        {
            var dbSetType = DbSetUnderlyingType(dbSetName);
            if (dbSetType is null) throw new Exception("dbSetType cannot be null!");
            var keys = GetKeysValues(sourceWithId, keyNames);
            var targetInDb = Find(dbSetType, keys?.ToArray()!)!;
            if (targetInDb is null) throw new Exception("body object with provided Keys not found in Database");
            return Update(dbSetType, targetInDb, sourceWithId, keyNames);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public object Update(string dbSetName, JsonElement sourceWithId, IEnumerable<string> keyNames)
    {
        try
        {
            var dbSetType = DbSetUnderlyingType(dbSetName);
            var sourceWithIdAsJson = sourceWithId.GetRawText();
            var expandoElement = JsonConvert.DeserializeObject<ExpandoObject>(sourceWithIdAsJson);
            var keyValues = GetKeysValues(expandoElement, keyNames);
            var whereClauses = keyNames.Zip(keyValues, (x, y) => $"{x} == {y}");
            var where = whereClauses.Aggregate((x, y) =>
            {
                if (y is not null) return $"{x} && {y}";
                return x;
            });
            var targetInDb = Query(dbSetName, where).Single();
            if (targetInDb is null) throw new Exception("body object with provided Keys not found in Database");
            return Update(dbSetType, targetInDb, expandoElement, keyNames);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }


    public IEnumerable<T>? GetAll<T>(string orderby, int page, int count, bool descending) where T : class
    {
        try
        {
            string direction = descending ? "desc" : "asc";
            return _context.Set<T>().OrderBy($"{orderby} {direction}").Skip((page - 1) * count).Take(count).AsEnumerable<T>();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public IQueryable<object>? GetAll(string dbSetName, string orderby, int page, int count, bool descending)
    {
        try
        {
            var prop = GetProperty(dbSetName);
            IQueryable<object>? collection = prop?.GetGetMethod()?.Invoke(_context, null) as IQueryable<object>;
            string direction = descending ? "desc" : "asc";
            return collection?.OrderBy($"{orderby} {direction}").Skip((page - 1) * count).Take(count);
        }
        catch (Exception)
        {
            throw;
        }
    }
    public IEnumerable<T>? GetAll<T>(string where, string orderby, int page, int count, bool descending) where T : class
    {
        try
        {
            string direction = descending ? "desc" : "asc";
            return _context.Set<T>().Where(where).OrderBy($"{orderby} {direction}").Skip((page - 1) * count).Take(count).AsEnumerable<T>();
        }
        catch (Exception)
        {
            throw;
        }
    }
    public IEnumerable<object>? GetAll(string dbSetName, string where, string orderby, int page, int count, bool descending)
    {
        try
        {
            string direction = descending ? "desc" : "asc";
            return Query(dbSetName, where)?.OrderBy($"{orderby} {direction}").Skip((page - 1) * count).Take(count).AsEnumerable<object>();
        }
        catch (Exception)
        {
            throw;
        }
    }
    public IQueryable<object>? GetAll(string dbSetName)
    {
        var prop = GetProperty(dbSetName);
        return prop?.GetGetMethod()?.Invoke(_context, null) as IQueryable<object>;
    }
    public IQueryable<object>? Query<T>(string where) where T : class => _context.Set<T>().Where(where);
    public IQueryable<object>? Query(string dbSetName, string where)
    {
        var prop = GetProperty(dbSetName);
        IQueryable<object>? collection = prop?.GetGetMethod()?.Invoke(_context, null) as IQueryable<object>;
        return collection?.Where(where);
    }

}