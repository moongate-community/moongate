namespace Moongate.Server.Services.Admin.Internal;

internal static class AdminRedisScripts
{
    public const string ReadGate = "return redis.call('HMGET',KEYS[1],'generation','blocked')";
    public const string OpenGate = "if redis.call('HGET',KEYS[1],'generation')~=ARGV[1] then return 0 end redis.call('HSET',KEYS[1],'blocked','0') return 1";
    public const string ResetGate = """
        local sessions=redis.call('ZRANGE',KEYS[2],0,63)
        -- Validate types before writes: Lua errors do not roll back previous commands.
        redis.call('HGET',KEYS[1],'generation')
        redis.call('HSET',KEYS[1],'generation',ARGV[1],'blocked',ARGV[2])
        redis.call('PERSIST',KEYS[1])
        for _,key in ipairs(sessions) do redis.call('DEL',key) end
        redis.call('DEL',KEYS[2])
        return 1
        """;
    public const string Issue = """
        local gate=redis.call('HMGET',KEYS[1],'generation','blocked')
        if gate[1]~=ARGV[1] or gate[2]~='0' then return -1 end
        if redis.call('EXISTS',KEYS[3])~=0 then return -1 end
        local time=redis.call('TIME')
        local now=time[1]*1000+math.floor(time[2]/1000)
        redis.call('ZREMRANGEBYSCORE',KEYS[2],'-inf',now)
        if redis.call('ZCARD',KEYS[2])>=64 then return -2 end
        local expires=now+tonumber(ARGV[5])
        redis.call('HSET',KEYS[3],'id',ARGV[2],'username',ARGV[3],'role',ARGV[4],'generation',ARGV[1],'expires',expires)
        redis.call('PEXPIREAT',KEYS[3],expires)
        redis.call('ZADD',KEYS[2],expires,KEYS[3])
        local newest=redis.call('ZRANGE',KEYS[2],-1,-1,'WITHSCORES')
        redis.call('PEXPIREAT',KEYS[2],math.floor(tonumber(newest[2])))
        return expires
        """;
    public const string Find = """
        local session=redis.call('HMGET',KEYS[1],'id','username','role','generation','expires')
        for i=1,5 do if not session[i] then return nil end end
        local expires=tonumber(session[5])
        if not expires then return nil end
        local time=redis.call('TIME')
        if expires<=time[1]*1000+math.floor(time[2]/1000) then return nil end
        local gate=redis.call('HMGET',ARGV[1]..'gate:'..session[1],'generation','blocked')
        if gate[1]~=session[4] or gate[2]~='0' then return nil end
        return session
        """;
    public const string Remove = """
        local id=redis.call('HGET',KEYS[1],'id')
        if id then redis.call('ZREM',ARGV[1]..'index:'..id,KEYS[1]) end
        return redis.call('DEL',KEYS[1])
        """;
    public const string Throttle = """
        local user=tonumber(redis.call('GET',KEYS[1]) or '0')
        local peer=tonumber(redis.call('GET',KEYS[2]) or '0')
        if not user or not peer then return redis.error_reply('Invalid throttle state') end
        if user>=10 or peer>=30 then return 0 end
        if redis.call('INCR',KEYS[1])==1 then redis.call('EXPIRE',KEYS[1],60) end
        if redis.call('INCR',KEYS[2])==1 then redis.call('EXPIRE',KEYS[2],60) end
        return 1
        """;
}
