const { UpdateFrequencies, UpdateTypes } = require("node-openttd-admin/enums");
const uuid = require('uuid');

var ottd = require("node-openttd-admin"),
  ottdConnection =  new ottd.connection();
 
ottdConnection.connect("localhost", 3977);
 
ottdConnection.on('connect', function(){
  ottdConnection.authenticate("BetterTTD-Bot", "p7gvv");
});

ottdConnection.on('welcome', function(data){
  ottdConnection.send_rcon("say \"hello world\"");
  ottdConnection.send_update_frequency(UpdateTypes.CHAT, UpdateFrequencies.AUTOMATIC);
  ottdConnection.close();
});

ottdConnection.on('chat', function(data) {
  console.log(data);
});

ottdConnection.on('error', function(error) {
  console.log(error);
});