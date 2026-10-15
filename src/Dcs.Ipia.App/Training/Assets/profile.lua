local diff = {
  ["keyDiffs"] = {
    ["d3001pnilu3001cd1vd1vpnilvu0"] = { ["name"] = "Sample landing light", ["added"] = { [1] = { ["key"] = "JOY_BTN4" } } },
    ["d3002pnilu3002cd1vd1vpnilvu0"] = { ["name"] = "Sample shifted action", ["added"] = { [1] = { ["key"] = "JOY_BTN5", ["reformers"] = { [1] = "TRAIN_SHIFT" } } } }
  },
  ["axisDiffs"] = {
    ["a2001cdnil"] = { ["name"] = "Sample pitch axis", ["added"] = { [1] = { ["key"] = "JOY_Y", ["filter"] = { ["deadzone"] = 2, ["curvature"] = { [1] = 10 } } } } },
    ["a2002cdnil"] = { ["name"] = "Captured default roll", ["removed"] = { [1] = { ["key"] = "JOY_X" } } }
  }
}
return diff
