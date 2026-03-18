local MOUNT_MOBILE_ID_PROP = "mount_mobile_id"
local MOUNT_TEMPLATE_ID = "ethereal_horse_mount"

local function resolve_mount(token, rider)
    local mount_id = tonumber(token:get_prop(MOUNT_MOBILE_ID_PROP) or 0)
    if mount_id ~= nil and mount_id > 0 then
        local existing_mount = mobile.get(mount_id)
        if existing_mount ~= nil and existing_mount.is_mountable then
            return existing_mount
        end

        token:set_prop(MOUNT_MOBILE_ID_PROP, 0)
    end

    local spawned_mount = mobile.spawn(MOUNT_TEMPLATE_ID, {
        x = rider.location_x,
        y = rider.location_y,
        z = rider.location_z,
        map_id = rider.map_id
    })
    if spawned_mount == nil then
        return nil
    end

    token:set_prop(MOUNT_MOBILE_ID_PROP, spawned_mount.serial)

    return spawned_mount
end

local function toggle_mount(ctx)
    if ctx == nil or ctx.item == nil or ctx.mobile_id == nil then
        return
    end

    local item_serial = tonumber(ctx.item.serial or 0)
    local rider_serial = tonumber(ctx.mobile_id or 0)
    if item_serial == nil or item_serial == 0 or rider_serial == nil or rider_serial == 0 then
        return
    end

    local token = item.get(item_serial)
    local rider = mobile.get(rider_serial)
    if token == nil or rider == nil then
        return
    end

    if mobile.dismount(rider_serial) then
        return
    end

    local mount = resolve_mount(token, rider)
    if mount == nil then
        return
    end

    if mount.map_id ~= rider.map_id or
        mount.location_x ~= rider.location_x or
        mount.location_y ~= rider.location_y or
        mount.location_z ~= rider.location_z then
        mount:teleport(rider.map_id, rider.location_x, rider.location_y, rider.location_z)
    end

    mobile.try_mount(rider.serial, mount.serial)
end

items_ethereal_horse = {
    on_double_click = toggle_mount,
}

items_gm_ethereal = items_ethereal_horse
