#nullable enable
namespace TH09.Analysis;

internal static class AnalysisTables
{
    public const int Sentinel = -999999;

    public const string RecordFieldsPacked = @"seq_begin
flags
p1_enemy_class_counts
p2_enemy_class_counts
mode
difficulty
stage_index
field_id
battle_bgm_id
round_frames
completed_rounds
rounds_required
pause_used
p1_wins
p2_wins
result_state
result_winner
input_mask
replay_flag
p1_character
p2_character
p1_control
p2_control
p1_cpu_level
p2_cpu_level
p1_life_raw
p2_life_raw
p1_lives
p2_lives
p1_score_raw
p2_score_raw
p1_score_mirror
p2_score_mirror
p1_current_combo
p2_current_combo
p1_max_combo
p2_max_combo
p1_spell_attacks
p2_spell_attacks
p1_boss_attacks
p2_boss_attacks
p1_boss_reversals
p2_boss_reversals
p1_spell_points
p2_spell_points
p1_spell_points_mirror
p2_spell_points_mirror
p1_gauge
p2_gauge
clear_life_bonus
clear_max_combo_bonus
clear_spell_bonus
clear_boss_bonus
clear_reversal_bonus
clear_lives_bonus
clear_total
p1_cpu_dodge_mode
p2_cpu_dodge_mode
p1_cpu_quick_timer
p2_cpu_quick_timer
p1_cpu_stand_timer
p2_cpu_stand_timer
p1_zero_hit_timer
p2_zero_hit_timer
p1_combo_gauge_raw
p2_combo_gauge_raw
p1_enemy_total
p2_enemy_total
p1_enemy_fairy
p2_enemy_fairy
p1_enemy_boss
p2_enemy_boss
p1_enemy_charge
p2_enemy_charge
p1_bullet_fairy
p2_bullet_fairy
p1_bullet_rival
p2_bullet_rival
internal_rank
rank_interval
rank_max
p1_charge
p2_charge
p1_card_attack_level
p2_card_attack_level
p1_boss_card_attack_level
p2_boss_card_attack_level
p1_boss_type
p2_boss_type
p1_boss_sub
p2_boss_sub
p1_boss_depth
p2_boss_depth
p1_boss_hp
p2_boss_hp
p1_ex_active
p2_ex_active
p1_ex_triggered
p2_ex_triggered
p1_cpu_quick_timer_cur
p2_cpu_quick_timer_cur
p1_cpu_stand_timer_cur
p2_cpu_stand_timer_cur
p1_combo_gauge_cur
p2_combo_gauge_cur
lily_counter
p1_pos_x
p2_pos_x
p1_pos_y
p2_pos_y
p1_input_replay
p2_input_replay
p1_white_bullet_points
p2_white_bullet_points
p1_ghost_points
p2_ghost_points
p1_ex_points
p2_ex_points
p1_boss_pos_x
p2_boss_pos_x
p1_boss_pos_y
p2_boss_pos_y
p1_cpu_charge_instruction
p2_cpu_charge_instruction
rng_state
p1_hit_kind
p1_hit_obj
p1_hit_x
p1_hit_y
p2_hit_kind
p2_hit_obj
p2_hit_x
p2_hit_y
hit_damage_base
coord_ring_state
os_interrupt_time_lo
os_tick_count_lo
p1_hit_obj_ptr
p2_hit_obj_ptr
p1_hit_laser_x
p1_hit_laser_y
p2_hit_laser_x
p2_hit_laser_y
p1_hit_laser_angle
p2_hit_laser_angle
p1_hit_elem_radius
p2_hit_elem_radius
p1_player_state
p2_player_state
p1_invincible_timer
p2_invincible_timer
p1_bullet_mgr
p2_bullet_mgr
p1_speed_mult
p2_speed_mult
p1_hit_list_count
p2_hit_list_count
p1_game_flags
p2_game_flags
global_state
p1_move_dir_angle
p2_move_dir_angle
p1_cpu_dir_lock
p2_cpu_dir_lock
p1_cpu_prev_dir
p2_cpu_prev_dir
p1_cpu_dir_hist0
p1_cpu_dir_hist1
p1_cpu_dir_hist2
p1_cpu_dir_hist3
p1_cpu_dir_hist4
p1_cpu_dir_hist5
p1_cpu_dir_hist6
p1_cpu_dir_hist7
p2_cpu_dir_hist0
p2_cpu_dir_hist1
p2_cpu_dir_hist2
p2_cpu_dir_hist3
p2_cpu_dir_hist4
p2_cpu_dir_hist5
p2_cpu_dir_hist6
p2_cpu_dir_hist7
p1_cpu_dodge_dir_hist0
p1_cpu_dodge_dir_hist1
p1_cpu_dodge_dir_hist2
p1_cpu_dodge_dir_hist3
p1_cpu_dodge_dir_hist4
p1_cpu_dodge_dir_hist5
p1_cpu_dodge_dir_hist6
p1_cpu_dodge_dir_hist7
p2_cpu_dodge_dir_hist0
p2_cpu_dodge_dir_hist1
p2_cpu_dodge_dir_hist2
p2_cpu_dodge_dir_hist3
p2_cpu_dodge_dir_hist4
p2_cpu_dodge_dir_hist5
p2_cpu_dodge_dir_hist6
p2_cpu_dodge_dir_hist7
p1_item0_kind
p1_item0_x
p1_item0_y
p1_item0_valid
p1_item1_kind
p1_item1_x
p1_item1_y
p1_item1_valid
p1_item2_kind
p1_item2_x
p1_item2_y
p1_item2_valid
p1_item3_kind
p1_item3_x
p1_item3_y
p1_item3_valid
p2_item0_kind
p2_item0_x
p2_item0_y
p2_item0_valid
p2_item1_kind
p2_item1_x
p2_item1_y
p2_item1_valid
p2_item2_kind
p2_item2_x
p2_item2_y
p2_item2_valid
p2_item3_kind
p2_item3_x
p2_item3_y
p2_item3_valid
p1_cpu_target_x
p2_cpu_target_x
p1_cpu_target_y
p2_cpu_target_y
p1_enemy_prio_x
p1_enemy_prio_y
p1_enemy_prio_flags
p2_enemy_prio_x
p2_enemy_prio_y
p2_enemy_prio_flags
p1_enemy_first_x
p1_enemy_first_y
p1_enemy_first_flags
p2_enemy_first_x
p2_enemy_first_y
p2_enemy_first_flags
p1_slow_mult_x
p2_slow_mult_x
p1_slow_mult_y
p2_slow_mult_y
p1_enemy_sub_mask
p2_enemy_sub_mask
seq_end";

    public const string DecodeSpecPacked = @"seq_begin	0	u32
flags	0	u32
p1_enemy_class_counts	1	u32
p2_enemy_class_counts	1	u32
mode	0	u32
difficulty	0	u32
stage_index	0	u32
field_id	0	u32
battle_bgm_id	0	u32
round_frames	0	u32
completed_rounds	0	u32
rounds_required	0	u32
pause_used	0	u32
p1_wins	1	u32
p2_wins	1	u32
result_state	2	u32
result_winner	2	u32
input_mask	0	u32
replay_flag	8	u32
p1_character	1	u32
p2_character	1	u32
p1_control	1	u32
p2_control	1	u32
p1_cpu_level	1	u32
p2_cpu_level	1	u32
p1_life_raw	1	u32
p2_life_raw	1	u32
p1_lives	4	f32
p2_lives	4	f32
p1_score_raw	4	u32
p2_score_raw	4	u32
p1_score_mirror	4	u32
p2_score_mirror	4	u32
p1_current_combo	1	u32
p2_current_combo	1	u32
p1_max_combo	1	u32
p2_max_combo	1	u32
p1_spell_attacks	1	u32
p2_spell_attacks	1	u32
p1_boss_attacks	1	u32
p2_boss_attacks	1	u32
p1_boss_reversals	1	u32
p2_boss_reversals	1	u32
p1_spell_points	1	u32
p2_spell_points	1	u32
p1_spell_points_mirror	1	u32
p2_spell_points_mirror	1	u32
p1_gauge	1	f32
p2_gauge	1	f32
clear_life_bonus	2	u32
clear_max_combo_bonus	2	u32
clear_spell_bonus	2	u32
clear_boss_bonus	2	u32
clear_reversal_bonus	2	u32
clear_lives_bonus	2	u32
clear_total	2	u32
p1_cpu_dodge_mode	1	s32
p2_cpu_dodge_mode	1	s32
p1_cpu_quick_timer	1	s32
p2_cpu_quick_timer	1	s32
p1_cpu_stand_timer	1	s32
p2_cpu_stand_timer	1	s32
p1_zero_hit_timer	1	s32
p2_zero_hit_timer	1	s32
p1_combo_gauge_raw	1	s32
p2_combo_gauge_raw	1	s32
p1_enemy_total	1	u32
p2_enemy_total	1	u32
p1_enemy_fairy	1	u32
p2_enemy_fairy	1	u32
p1_enemy_boss	1	u32
p2_enemy_boss	1	u32
p1_enemy_charge	1	u32
p2_enemy_charge	1	u32
p1_bullet_fairy	1	u32
p2_bullet_fairy	1	u32
p1_bullet_rival	1	u32
p2_bullet_rival	1	u32
internal_rank	0	u32
rank_interval	0	u32
rank_max	0	u32
p1_charge	1	f32
p2_charge	1	f32
p1_card_attack_level	1	u32
p2_card_attack_level	1	u32
p1_boss_card_attack_level	1	u32
p2_boss_card_attack_level	1	u32
p1_boss_type	1	u32
p2_boss_type	1	u32
p1_boss_sub	1	u32
p2_boss_sub	1	u32
p1_boss_depth	1	u32
p2_boss_depth	1	u32
p1_boss_hp	1	u32
p2_boss_hp	1	u32
p1_ex_active	1	u32
p2_ex_active	1	u32
p1_ex_triggered	1	u32
p2_ex_triggered	1	u32
p1_cpu_quick_timer_cur	1	s32
p2_cpu_quick_timer_cur	1	s32
p1_cpu_stand_timer_cur	1	s32
p2_cpu_stand_timer_cur	1	s32
p1_combo_gauge_cur	1	s32
p2_combo_gauge_cur	1	s32
lily_counter	0	u32
p1_pos_x	1	f32
p2_pos_x	1	f32
p1_pos_y	1	f32
p2_pos_y	1	f32
p1_input_replay	1	u32
p2_input_replay	1	u32
p1_white_bullet_points	1	s32
p2_white_bullet_points	1	s32
p1_ghost_points	1	s32
p2_ghost_points	1	s32
p1_ex_points	1	s32
p2_ex_points	1	s32
p1_boss_pos_x	1	f32
p2_boss_pos_x	1	f32
p1_boss_pos_y	1	f32
p2_boss_pos_y	1	f32
p1_cpu_charge_instruction	1	f32
p2_cpu_charge_instruction	1	f32
rng_state	0	u32
p1_hit_kind	1	u32
p1_hit_obj	1	u32
p1_hit_x	1	f32
p1_hit_y	1	f32
p2_hit_kind	1	u32
p2_hit_obj	1	u32
p2_hit_x	1	f32
p2_hit_y	1	f32
hit_damage_base	0	u32
coord_ring_state	0	u32
os_interrupt_time_lo	0	u32
os_tick_count_lo	0	u32
p1_hit_obj_ptr	1	u32
p2_hit_obj_ptr	1	u32
p1_hit_laser_x	1	f32
p1_hit_laser_y	1	f32
p2_hit_laser_x	1	f32
p2_hit_laser_y	1	f32
p1_hit_laser_angle	1	f32
p2_hit_laser_angle	1	f32
p1_hit_elem_radius	1	f32
p2_hit_elem_radius	1	f32
p1_player_state	1	u32
p2_player_state	1	u32
p1_invincible_timer	1	s32
p2_invincible_timer	1	s32
p1_bullet_mgr	1	u32
p2_bullet_mgr	1	u32
p1_speed_mult	1	f32
p2_speed_mult	1	f32
p1_hit_list_count	1	u32
p2_hit_list_count	1	u32
p1_game_flags	1	u32
p2_game_flags	1	u32
global_state	0	u32
p1_move_dir_angle	1	f32
p2_move_dir_angle	1	f32
p1_cpu_dir_lock	1	u32
p2_cpu_dir_lock	1	u32
p1_cpu_prev_dir	1	u32
p2_cpu_prev_dir	1	u32
p1_cpu_dir_hist0	1	u32
p1_cpu_dir_hist1	1	u32
p1_cpu_dir_hist2	1	u32
p1_cpu_dir_hist3	1	u32
p1_cpu_dir_hist4	1	u32
p1_cpu_dir_hist5	1	u32
p1_cpu_dir_hist6	1	u32
p1_cpu_dir_hist7	1	u32
p2_cpu_dir_hist0	1	u32
p2_cpu_dir_hist1	1	u32
p2_cpu_dir_hist2	1	u32
p2_cpu_dir_hist3	1	u32
p2_cpu_dir_hist4	1	u32
p2_cpu_dir_hist5	1	u32
p2_cpu_dir_hist6	1	u32
p2_cpu_dir_hist7	1	u32
p1_cpu_dodge_dir_hist0	1	u32
p1_cpu_dodge_dir_hist1	1	u32
p1_cpu_dodge_dir_hist2	1	u32
p1_cpu_dodge_dir_hist3	1	u32
p1_cpu_dodge_dir_hist4	1	u32
p1_cpu_dodge_dir_hist5	1	u32
p1_cpu_dodge_dir_hist6	1	u32
p1_cpu_dodge_dir_hist7	1	u32
p2_cpu_dodge_dir_hist0	1	u32
p2_cpu_dodge_dir_hist1	1	u32
p2_cpu_dodge_dir_hist2	1	u32
p2_cpu_dodge_dir_hist3	1	u32
p2_cpu_dodge_dir_hist4	1	u32
p2_cpu_dodge_dir_hist5	1	u32
p2_cpu_dodge_dir_hist6	1	u32
p2_cpu_dodge_dir_hist7	1	u32
p1_item0_kind	1	u32
p1_item0_x	1	f32
p1_item0_y	1	f32
p1_item0_valid	1	u32
p1_item1_kind	1	u32
p1_item1_x	1	f32
p1_item1_y	1	f32
p1_item1_valid	1	u32
p1_item2_kind	1	u32
p1_item2_x	1	f32
p1_item2_y	1	f32
p1_item2_valid	1	u32
p1_item3_kind	1	u32
p1_item3_x	1	f32
p1_item3_y	1	f32
p1_item3_valid	1	u32
p2_item0_kind	1	u32
p2_item0_x	1	f32
p2_item0_y	1	f32
p2_item0_valid	1	u32
p2_item1_kind	1	u32
p2_item1_x	1	f32
p2_item1_y	1	f32
p2_item1_valid	1	u32
p2_item2_kind	1	u32
p2_item2_x	1	f32
p2_item2_y	1	f32
p2_item2_valid	1	u32
p2_item3_kind	1	u32
p2_item3_x	1	f32
p2_item3_y	1	f32
p2_item3_valid	1	u32
p1_cpu_target_x	1	f32
p2_cpu_target_x	1	f32
p1_cpu_target_y	1	f32
p2_cpu_target_y	1	f32
p1_enemy_prio_x	1	f32
p1_enemy_prio_y	1	f32
p1_enemy_prio_flags	1	u32
p2_enemy_prio_x	1	f32
p2_enemy_prio_y	1	f32
p2_enemy_prio_flags	1	u32
p1_enemy_first_x	1	f32
p1_enemy_first_y	1	f32
p1_enemy_first_flags	1	u32
p2_enemy_first_x	1	f32
p2_enemy_first_y	1	f32
p2_enemy_first_flags	1	u32
p1_slow_mult_x	1	f32
p2_slow_mult_x	1	f32
p1_slow_mult_y	1	f32
p2_slow_mult_y	1	f32
p1_enemy_sub_mask	1	u32
p2_enemy_sub_mask	1	u32
seq_end	0	u32";

    public const int FloatFieldCount = 64;
    public const int SignedFieldCount = 24;
    public const int FlagGroupCount = 4;

    public const string SlotBasesPacked = @"p1_b0
p1_b1
p1_b2
p1_b3
p1_b4
p1_b5
p1_b6
p1_b7
p1_b8
p1_b9
p1_b10
p1_b11
p1_b12
p1_b13
p1_b14
p1_b15
p1_b16
p1_b17
p1_b18
p1_b19
p1_b20
p1_b21
p1_b22
p1_b23
p1_b24
p1_b25
p1_b26
p1_b27
p1_b28
p1_b29
p1_b30
p1_b31
p1_b32
p1_b33
p1_b34
p1_b35
p1_b36
p1_b37
p1_b38
p1_b39
p1_b40
p1_b41
p1_b42
p1_b43
p1_b44
p1_b45
p1_b46
p1_b47
p1_b48
p1_b49
p1_b50
p1_b51
p1_b52
p1_b53
p1_b54
p1_b55
p1_b56
p1_b57
p1_b58
p1_b59
p1_b60
p1_b61
p1_b62
p1_b63
p1_b64
p1_b65
p1_b66
p1_b67
p1_b68
p1_b69
p1_b70
p1_b71
p1_b72
p1_b73
p1_b74
p1_b75
p1_b76
p1_b77
p1_b78
p1_b79
p1_b80
p1_b81
p1_b82
p1_b83
p1_b84
p1_b85
p1_b86
p1_b87
p1_b88
p1_b89
p1_b90
p1_b91
p1_b92
p1_b93
p1_b94
p1_b95
p1_b96
p1_b97
p1_b98
p1_b99
p1_b100
p1_b101
p1_b102
p1_b103
p1_b104
p1_b105
p1_b106
p1_b107
p1_b108
p1_b109
p1_b110
p1_b111
p1_b112
p1_b113
p1_b114
p1_b115
p1_b116
p1_b117
p1_b118
p1_b119
p1_b120
p1_b121
p1_b122
p1_b123
p1_b124
p1_b125
p1_b126
p1_b127
p1_b128
p1_b129
p1_b130
p1_b131
p1_b132
p1_b133
p1_b134
p1_b135
p1_b136
p1_b137
p1_b138
p1_b139
p1_b140
p1_b141
p1_b142
p1_b143
p1_b144
p1_b145
p1_b146
p1_b147
p1_b148
p1_b149
p1_b150
p1_b151
p1_b152
p1_b153
p1_b154
p1_b155
p1_b156
p1_b157
p1_b158
p1_b159
p1_b160
p1_b161
p1_b162
p1_b163
p1_b164
p1_b165
p1_b166
p1_b167
p1_b168
p1_b169
p1_b170
p1_b171
p1_b172
p1_b173
p1_b174
p1_b175
p1_b176
p1_b177
p1_b178
p1_b179
p1_b180
p1_b181
p1_b182
p1_b183
p1_b184
p1_b185
p1_b186
p1_b187
p1_b188
p1_b189
p1_b190
p1_b191
p1_b192
p1_b193
p1_b194
p1_b195
p1_b196
p1_b197
p1_b198
p1_b199
p1_b200
p1_b201
p1_b202
p1_b203
p1_b204
p1_b205
p1_b206
p1_b207
p1_b208
p1_b209
p1_b210
p1_b211
p1_b212
p1_b213
p1_b214
p1_b215
p1_b216
p1_b217
p1_b218
p1_b219
p1_b220
p1_b221
p1_b222
p1_b223
p1_b224
p1_b225
p1_b226
p1_b227
p1_b228
p1_b229
p1_b230
p1_b231
p1_b232
p1_b233
p1_b234
p1_b235
p1_b236
p1_b237
p1_b238
p1_b239
p1_b240
p1_b241
p1_b242
p1_b243
p1_b244
p1_b245
p1_b246
p1_b247
p1_b248
p1_b249
p1_b250
p1_b251
p1_b252
p1_b253
p1_b254
p1_b255
p1_b256
p1_b257
p1_b258
p1_b259
p1_b260
p1_b261
p1_b262
p1_b263
p1_b264
p1_b265
p1_b266
p1_b267
p1_b268
p1_b269
p1_b270
p1_b271
p1_b272
p1_b273
p1_b274
p1_b275
p1_b276
p1_b277
p1_b278
p1_b279
p1_b280
p1_b281
p1_b282
p1_b283
p1_b284
p1_b285
p1_b286
p1_b287
p1_b288
p1_b289
p1_b290
p1_b291
p1_b292
p1_b293
p1_b294
p1_b295
p1_b296
p1_b297
p1_b298
p1_b299
p1_b300
p1_b301
p1_b302
p1_b303
p1_b304
p1_b305
p1_b306
p1_b307
p1_b308
p1_b309
p1_b310
p1_b311
p1_b312
p1_b313
p1_b314
p1_b315
p1_b316
p1_b317
p1_b318
p1_b319
p1_b320
p1_b321
p1_b322
p1_b323
p1_b324
p1_b325
p1_b326
p1_b327
p1_b328
p1_b329
p1_b330
p1_b331
p1_b332
p1_b333
p1_b334
p1_b335
p1_b336
p1_b337
p1_b338
p1_b339
p1_b340
p1_b341
p1_b342
p1_b343
p1_b344
p1_b345
p1_b346
p1_b347
p1_b348
p1_b349
p1_b350
p1_b351
p1_b352
p1_b353
p1_b354
p1_b355
p1_b356
p1_b357
p1_b358
p1_b359
p1_b360
p1_b361
p1_b362
p1_b363
p1_b364
p1_b365
p1_b366
p1_b367
p1_b368
p1_b369
p1_b370
p1_b371
p1_b372
p1_b373
p1_b374
p1_b375
p1_b376
p1_b377
p1_b378
p1_b379
p1_b380
p1_b381
p1_b382
p1_b383
p1_b384
p1_b385
p1_b386
p1_b387
p1_b388
p1_b389
p1_b390
p1_b391
p1_b392
p1_b393
p1_b394
p1_b395
p1_b396
p1_b397
p1_b398
p1_b399
p1_b400
p1_b401
p1_b402
p1_b403
p1_b404
p1_b405
p1_b406
p1_b407
p1_b408
p1_b409
p1_b410
p1_b411
p1_b412
p1_b413
p1_b414
p1_b415
p1_b416
p1_b417
p1_b418
p1_b419
p1_b420
p1_b421
p1_b422
p1_b423
p1_b424
p1_b425
p1_b426
p1_b427
p1_b428
p1_b429
p1_b430
p1_b431
p1_b432
p1_b433
p1_b434
p1_b435
p1_b436
p1_b437
p1_b438
p1_b439
p1_b440
p1_b441
p1_b442
p1_b443
p1_b444
p1_b445
p1_b446
p1_b447
p1_b448
p1_b449
p1_b450
p1_b451
p1_b452
p1_b453
p1_b454
p1_b455
p1_b456
p1_b457
p1_b458
p1_b459
p1_b460
p1_b461
p1_b462
p1_b463
p1_b464
p1_b465
p1_b466
p1_b467
p1_b468
p1_b469
p1_b470
p1_b471
p1_b472
p1_b473
p1_b474
p1_b475
p1_b476
p1_b477
p1_b478
p1_b479
p1_b480
p1_b481
p1_b482
p1_b483
p1_b484
p1_b485
p1_b486
p1_b487
p1_b488
p1_b489
p1_b490
p1_b491
p1_b492
p1_b493
p1_b494
p1_b495
p1_b496
p1_b497
p1_b498
p1_b499
p1_b500
p1_b501
p1_b502
p1_b503
p1_b504
p1_b505
p1_b506
p1_b507
p1_b508
p1_b509
p1_b510
p1_b511
p1_b512
p1_b513
p1_b514
p1_b515
p1_b516
p1_b517
p1_b518
p1_b519
p1_b520
p1_b521
p1_b522
p1_b523
p1_b524
p1_b525
p1_b526
p1_b527
p1_b528
p1_b529
p1_b530
p1_b531
p1_b532
p1_b533
p1_b534
p1_b535
p1_b536
p1_e0
p1_e1
p1_e2
p1_e3
p1_e4
p1_e5
p1_e6
p1_e7
p1_e8
p1_e9
p1_e10
p1_e11
p1_e12
p1_e13
p1_e14
p1_e15
p1_e16
p1_e17
p1_e18
p1_e19
p1_e20
p1_e21
p1_e22
p1_e23
p1_e24
p1_e25
p1_e26
p1_e27
p1_e28
p1_e29
p1_e30
p1_e31
p1_e32
p1_e33
p1_e34
p1_e35
p1_e36
p1_e37
p1_e38
p1_e39
p1_e40
p1_e41
p1_e42
p1_e43
p1_e44
p1_e45
p1_e46
p1_e47
p1_e48
p1_e49
p1_e50
p1_e51
p1_e52
p1_e53
p1_e54
p1_e55
p1_e56
p1_e57
p1_e58
p1_e59
p1_e60
p1_e61
p1_e62
p1_e63
p1_e64
p1_e65
p1_e66
p1_e67
p1_e68
p1_e69
p1_e70
p1_e71
p1_e72
p1_e73
p1_e74
p1_e75
p1_e76
p1_e77
p1_e78
p1_e79
p1_e80
p1_e81
p1_e82
p1_e83
p1_e84
p1_e85
p1_e86
p1_e87
p1_e88
p1_e89
p1_e90
p1_e91
p1_e92
p1_e93
p1_e94
p1_e95
p1_e96
p1_e97
p1_e98
p1_e99
p1_e100
p1_e101
p1_e102
p1_e103
p1_e104
p1_e105
p1_e106
p1_e107
p1_e108
p1_e109
p1_e110
p1_e111
p1_e112
p1_e113
p1_e114
p1_e115
p1_e116
p1_e117
p1_e118
p1_e119
p1_e120
p1_e121
p1_e122
p1_e123
p1_e124
p1_e125
p1_e126
p1_e127
p2_b0
p2_b1
p2_b2
p2_b3
p2_b4
p2_b5
p2_b6
p2_b7
p2_b8
p2_b9
p2_b10
p2_b11
p2_b12
p2_b13
p2_b14
p2_b15
p2_b16
p2_b17
p2_b18
p2_b19
p2_b20
p2_b21
p2_b22
p2_b23
p2_b24
p2_b25
p2_b26
p2_b27
p2_b28
p2_b29
p2_b30
p2_b31
p2_b32
p2_b33
p2_b34
p2_b35
p2_b36
p2_b37
p2_b38
p2_b39
p2_b40
p2_b41
p2_b42
p2_b43
p2_b44
p2_b45
p2_b46
p2_b47
p2_b48
p2_b49
p2_b50
p2_b51
p2_b52
p2_b53
p2_b54
p2_b55
p2_b56
p2_b57
p2_b58
p2_b59
p2_b60
p2_b61
p2_b62
p2_b63
p2_b64
p2_b65
p2_b66
p2_b67
p2_b68
p2_b69
p2_b70
p2_b71
p2_b72
p2_b73
p2_b74
p2_b75
p2_b76
p2_b77
p2_b78
p2_b79
p2_b80
p2_b81
p2_b82
p2_b83
p2_b84
p2_b85
p2_b86
p2_b87
p2_b88
p2_b89
p2_b90
p2_b91
p2_b92
p2_b93
p2_b94
p2_b95
p2_b96
p2_b97
p2_b98
p2_b99
p2_b100
p2_b101
p2_b102
p2_b103
p2_b104
p2_b105
p2_b106
p2_b107
p2_b108
p2_b109
p2_b110
p2_b111
p2_b112
p2_b113
p2_b114
p2_b115
p2_b116
p2_b117
p2_b118
p2_b119
p2_b120
p2_b121
p2_b122
p2_b123
p2_b124
p2_b125
p2_b126
p2_b127
p2_b128
p2_b129
p2_b130
p2_b131
p2_b132
p2_b133
p2_b134
p2_b135
p2_b136
p2_b137
p2_b138
p2_b139
p2_b140
p2_b141
p2_b142
p2_b143
p2_b144
p2_b145
p2_b146
p2_b147
p2_b148
p2_b149
p2_b150
p2_b151
p2_b152
p2_b153
p2_b154
p2_b155
p2_b156
p2_b157
p2_b158
p2_b159
p2_b160
p2_b161
p2_b162
p2_b163
p2_b164
p2_b165
p2_b166
p2_b167
p2_b168
p2_b169
p2_b170
p2_b171
p2_b172
p2_b173
p2_b174
p2_b175
p2_b176
p2_b177
p2_b178
p2_b179
p2_b180
p2_b181
p2_b182
p2_b183
p2_b184
p2_b185
p2_b186
p2_b187
p2_b188
p2_b189
p2_b190
p2_b191
p2_b192
p2_b193
p2_b194
p2_b195
p2_b196
p2_b197
p2_b198
p2_b199
p2_b200
p2_b201
p2_b202
p2_b203
p2_b204
p2_b205
p2_b206
p2_b207
p2_b208
p2_b209
p2_b210
p2_b211
p2_b212
p2_b213
p2_b214
p2_b215
p2_b216
p2_b217
p2_b218
p2_b219
p2_b220
p2_b221
p2_b222
p2_b223
p2_b224
p2_b225
p2_b226
p2_b227
p2_b228
p2_b229
p2_b230
p2_b231
p2_b232
p2_b233
p2_b234
p2_b235
p2_b236
p2_b237
p2_b238
p2_b239
p2_b240
p2_b241
p2_b242
p2_b243
p2_b244
p2_b245
p2_b246
p2_b247
p2_b248
p2_b249
p2_b250
p2_b251
p2_b252
p2_b253
p2_b254
p2_b255
p2_b256
p2_b257
p2_b258
p2_b259
p2_b260
p2_b261
p2_b262
p2_b263
p2_b264
p2_b265
p2_b266
p2_b267
p2_b268
p2_b269
p2_b270
p2_b271
p2_b272
p2_b273
p2_b274
p2_b275
p2_b276
p2_b277
p2_b278
p2_b279
p2_b280
p2_b281
p2_b282
p2_b283
p2_b284
p2_b285
p2_b286
p2_b287
p2_b288
p2_b289
p2_b290
p2_b291
p2_b292
p2_b293
p2_b294
p2_b295
p2_b296
p2_b297
p2_b298
p2_b299
p2_b300
p2_b301
p2_b302
p2_b303
p2_b304
p2_b305
p2_b306
p2_b307
p2_b308
p2_b309
p2_b310
p2_b311
p2_b312
p2_b313
p2_b314
p2_b315
p2_b316
p2_b317
p2_b318
p2_b319
p2_b320
p2_b321
p2_b322
p2_b323
p2_b324
p2_b325
p2_b326
p2_b327
p2_b328
p2_b329
p2_b330
p2_b331
p2_b332
p2_b333
p2_b334
p2_b335
p2_b336
p2_b337
p2_b338
p2_b339
p2_b340
p2_b341
p2_b342
p2_b343
p2_b344
p2_b345
p2_b346
p2_b347
p2_b348
p2_b349
p2_b350
p2_b351
p2_b352
p2_b353
p2_b354
p2_b355
p2_b356
p2_b357
p2_b358
p2_b359
p2_b360
p2_b361
p2_b362
p2_b363
p2_b364
p2_b365
p2_b366
p2_b367
p2_b368
p2_b369
p2_b370
p2_b371
p2_b372
p2_b373
p2_b374
p2_b375
p2_b376
p2_b377
p2_b378
p2_b379
p2_b380
p2_b381
p2_b382
p2_b383
p2_b384
p2_b385
p2_b386
p2_b387
p2_b388
p2_b389
p2_b390
p2_b391
p2_b392
p2_b393
p2_b394
p2_b395
p2_b396
p2_b397
p2_b398
p2_b399
p2_b400
p2_b401
p2_b402
p2_b403
p2_b404
p2_b405
p2_b406
p2_b407
p2_b408
p2_b409
p2_b410
p2_b411
p2_b412
p2_b413
p2_b414
p2_b415
p2_b416
p2_b417
p2_b418
p2_b419
p2_b420
p2_b421
p2_b422
p2_b423
p2_b424
p2_b425
p2_b426
p2_b427
p2_b428
p2_b429
p2_b430
p2_b431
p2_b432
p2_b433
p2_b434
p2_b435
p2_b436
p2_b437
p2_b438
p2_b439
p2_b440
p2_b441
p2_b442
p2_b443
p2_b444
p2_b445
p2_b446
p2_b447
p2_b448
p2_b449
p2_b450
p2_b451
p2_b452
p2_b453
p2_b454
p2_b455
p2_b456
p2_b457
p2_b458
p2_b459
p2_b460
p2_b461
p2_b462
p2_b463
p2_b464
p2_b465
p2_b466
p2_b467
p2_b468
p2_b469
p2_b470
p2_b471
p2_b472
p2_b473
p2_b474
p2_b475
p2_b476
p2_b477
p2_b478
p2_b479
p2_b480
p2_b481
p2_b482
p2_b483
p2_b484
p2_b485
p2_b486
p2_b487
p2_b488
p2_b489
p2_b490
p2_b491
p2_b492
p2_b493
p2_b494
p2_b495
p2_b496
p2_b497
p2_b498
p2_b499
p2_b500
p2_b501
p2_b502
p2_b503
p2_b504
p2_b505
p2_b506
p2_b507
p2_b508
p2_b509
p2_b510
p2_b511
p2_b512
p2_b513
p2_b514
p2_b515
p2_b516
p2_b517
p2_b518
p2_b519
p2_b520
p2_b521
p2_b522
p2_b523
p2_b524
p2_b525
p2_b526
p2_b527
p2_b528
p2_b529
p2_b530
p2_b531
p2_b532
p2_b533
p2_b534
p2_b535
p2_b536
p2_e0
p2_e1
p2_e2
p2_e3
p2_e4
p2_e5
p2_e6
p2_e7
p2_e8
p2_e9
p2_e10
p2_e11
p2_e12
p2_e13
p2_e14
p2_e15
p2_e16
p2_e17
p2_e18
p2_e19
p2_e20
p2_e21
p2_e22
p2_e23
p2_e24
p2_e25
p2_e26
p2_e27
p2_e28
p2_e29
p2_e30
p2_e31
p2_e32
p2_e33
p2_e34
p2_e35
p2_e36
p2_e37
p2_e38
p2_e39
p2_e40
p2_e41
p2_e42
p2_e43
p2_e44
p2_e45
p2_e46
p2_e47
p2_e48
p2_e49
p2_e50
p2_e51
p2_e52
p2_e53
p2_e54
p2_e55
p2_e56
p2_e57
p2_e58
p2_e59
p2_e60
p2_e61
p2_e62
p2_e63
p2_e64
p2_e65
p2_e66
p2_e67
p2_e68
p2_e69
p2_e70
p2_e71
p2_e72
p2_e73
p2_e74
p2_e75
p2_e76
p2_e77
p2_e78
p2_e79
p2_e80
p2_e81
p2_e82
p2_e83
p2_e84
p2_e85
p2_e86
p2_e87
p2_e88
p2_e89
p2_e90
p2_e91
p2_e92
p2_e93
p2_e94
p2_e95
p2_e96
p2_e97
p2_e98
p2_e99
p2_e100
p2_e101
p2_e102
p2_e103
p2_e104
p2_e105
p2_e106
p2_e107
p2_e108
p2_e109
p2_e110
p2_e111
p2_e112
p2_e113
p2_e114
p2_e115
p2_e116
p2_e117
p2_e118
p2_e119
p2_e120
p2_e121
p2_e122
p2_e123
p2_e124
p2_e125
p2_e126
p2_e127
p1_l0
p1_l1
p1_l2
p1_l3
p1_l4
p1_l5
p1_l6
p1_l7
p1_l8
p1_l9
p1_l10
p1_l11
p1_l12
p1_l13
p1_l14
p1_l15
p1_l16
p1_l17
p1_l18
p1_l19
p1_l20
p1_l21
p1_l22
p1_l23
p1_l24
p1_l25
p1_l26
p1_l27
p1_l28
p1_l29
p1_l30
p1_l31
p1_l32
p1_l33
p1_l34
p1_l35
p1_l36
p1_l37
p1_l38
p1_l39
p1_l40
p1_l41
p1_l42
p1_l43
p1_l44
p1_l45
p1_l46
p1_l47
p2_l0
p2_l1
p2_l2
p2_l3
p2_l4
p2_l5
p2_l6
p2_l7
p2_l8
p2_l9
p2_l10
p2_l11
p2_l12
p2_l13
p2_l14
p2_l15
p2_l16
p2_l17
p2_l18
p2_l19
p2_l20
p2_l21
p2_l22
p2_l23
p2_l24
p2_l25
p2_l26
p2_l27
p2_l28
p2_l29
p2_l30
p2_l31
p2_l32
p2_l33
p2_l34
p2_l35
p2_l36
p2_l37
p2_l38
p2_l39
p2_l40
p2_l41
p2_l42
p2_l43
p2_l44
p2_l45
p2_l46
p2_l47
ex0
ex1
ex2
ex3
ex4
ex5
ex6
ex7
ex8
ex9
ex10
ex11
ex12
ex13
ex14
ex15
ex16
ex17
ex18
ex19
ex20
ex21
ex22
ex23
ex24
ex25
ex26
ex27
ex28
ex29
ex30
ex31
ex32
ex33
ex34
ex35
ex36
ex37
ex38
ex39
ex40
ex41
ex42
ex43
ex44
ex45
ex46
ex47
ex48
ex49
ex50
ex51
ex52
ex53
ex54
ex55
ex56
ex57
ex58
ex59
ex60
ex61
ex62
ex63
ex64
ex65
ex66
ex67
ex68
ex69
ex70
ex71
ex72
ex73
ex74
ex75
ex76
ex77
ex78
ex79
ex80
ex81
ex82
ex83
ex84
ex85
ex86
ex87
ex88
ex89
ex90
ex91
ex92
ex93
ex94
ex95
ex96
ex97
ex98
ex99
ex100
ex101
ex102
ex103
ex104
ex105
ex106
ex107
ex108
ex109
ex110
ex111
ex112
ex113
ex114
ex115
ex116
ex117
ex118
ex119
ex120
ex121
ex122
ex123
ex124
ex125
ex126
ex127
ex128
ex129
ex130
ex131
ex132
ex133
ex134
ex135
ex136
ex137
ex138
ex139
ex140
ex141
ex142
ex143
ex144
ex145
ex146
ex147
ex148
ex149
ex150
ex151
ex152
ex153
ex154
ex155
ex156
ex157
ex158
ex159
ex160
ex161
ex162
ex163
ex164
ex165
ex166
ex167
ex168
ex169
ex170
ex171
ex172
ex173
ex174
ex175
ex176
ex177
ex178
ex179
ex180
ex181
ex182
ex183
ex184
ex185
ex186
ex187
ex188
ex189
ex190
ex191
ex192
ex193
ex194
ex195
ex196
ex197
ex198
ex199
ex200
ex201
ex202
ex203
ex204
ex205
ex206
ex207
ex208
ex209
ex210
ex211
ex212
ex213
ex214
ex215
ex216
ex217
ex218
ex219
ex220
ex221
ex222
ex223
ex224
ex225
ex226
ex227
ex228
ex229
ex230
ex231
ex232
ex233
ex234
ex235
ex236
ex237
ex238
ex239
ex240
ex241
ex242
ex243
ex244
ex245
ex246
ex247
ex248
ex249
ex250
ex251
ex252
ex253
ex254
ex255
p1_s0
p1_s1
p1_s2
p1_s3
p1_s4
p1_s5
p1_s6
p1_s7
p1_s8
p1_s9
p1_s10
p1_s11
p1_s12
p1_s13
p1_s14
p1_s15
p1_s16
p1_s17
p1_s18
p1_s19
p1_s20
p1_s21
p1_s22
p1_s23
p1_s24
p1_s25
p1_s26
p1_s27
p1_s28
p1_s29
p1_s30
p1_s31
p1_s32
p1_s33
p1_s34
p1_s35
p1_s36
p1_s37
p1_s38
p1_s39
p1_s40
p1_s41
p1_s42
p1_s43
p1_s44
p1_s45
p1_s46
p1_s47
p1_s48
p1_s49
p1_s50
p1_s51
p1_s52
p1_s53
p1_s54
p1_s55
p1_s56
p1_s57
p1_s58
p1_s59
p1_s60
p1_s61
p1_s62
p1_s63
p1_s64
p1_s65
p1_s66
p1_s67
p1_s68
p1_s69
p1_s70
p1_s71
p1_s72
p1_s73
p1_s74
p1_s75
p1_s76
p1_s77
p1_s78
p1_s79
p1_s80
p1_s81
p1_s82
p1_s83
p1_s84
p1_s85
p1_s86
p1_s87
p1_s88
p1_s89
p1_s90
p1_s91
p1_s92
p1_s93
p1_s94
p1_s95
p1_s96
p1_s97
p1_s98
p1_s99
p1_s100
p1_s101
p1_s102
p1_s103
p1_s104
p1_s105
p1_s106
p1_s107
p1_s108
p1_s109
p1_s110
p1_s111
p1_s112
p1_s113
p1_s114
p1_s115
p1_s116
p1_s117
p1_s118
p1_s119
p1_s120
p1_s121
p1_s122
p1_s123
p1_s124
p1_s125
p1_s126
p1_s127
p2_s0
p2_s1
p2_s2
p2_s3
p2_s4
p2_s5
p2_s6
p2_s7
p2_s8
p2_s9
p2_s10
p2_s11
p2_s12
p2_s13
p2_s14
p2_s15
p2_s16
p2_s17
p2_s18
p2_s19
p2_s20
p2_s21
p2_s22
p2_s23
p2_s24
p2_s25
p2_s26
p2_s27
p2_s28
p2_s29
p2_s30
p2_s31
p2_s32
p2_s33
p2_s34
p2_s35
p2_s36
p2_s37
p2_s38
p2_s39
p2_s40
p2_s41
p2_s42
p2_s43
p2_s44
p2_s45
p2_s46
p2_s47
p2_s48
p2_s49
p2_s50
p2_s51
p2_s52
p2_s53
p2_s54
p2_s55
p2_s56
p2_s57
p2_s58
p2_s59
p2_s60
p2_s61
p2_s62
p2_s63
p2_s64
p2_s65
p2_s66
p2_s67
p2_s68
p2_s69
p2_s70
p2_s71
p2_s72
p2_s73
p2_s74
p2_s75
p2_s76
p2_s77
p2_s78
p2_s79
p2_s80
p2_s81
p2_s82
p2_s83
p2_s84
p2_s85
p2_s86
p2_s87
p2_s88
p2_s89
p2_s90
p2_s91
p2_s92
p2_s93
p2_s94
p2_s95
p2_s96
p2_s97
p2_s98
p2_s99
p2_s100
p2_s101
p2_s102
p2_s103
p2_s104
p2_s105
p2_s106
p2_s107
p2_s108
p2_s109
p2_s110
p2_s111
p2_s112
p2_s113
p2_s114
p2_s115
p2_s116
p2_s117
p2_s118
p2_s119
p2_s120
p2_s121
p2_s122
p2_s123
p2_s124
p2_s125
p2_s126
p2_s127";

    public const string DisplayKindsPacked = @"seq_begin	INTERNAL
flags	INTERNAL
p1_enemy_class_counts	LINE
p2_enemy_class_counts	LINE
mode	LABEL
difficulty	LABEL
stage_index	LABEL
field_id	LABEL
battle_bgm_id	LABEL
round_frames	AXIS
completed_rounds	INTERNAL
rounds_required	NONE
pause_used	LABEL
p1_wins	LABEL
p2_wins	LABEL
result_state	INTERNAL
result_winner	LABEL
input_mask	NONE
replay_flag	NONE
p1_character	LABEL
p2_character	LABEL
p1_control	LABEL
p2_control	LABEL
p1_cpu_level	LABEL
p2_cpu_level	LABEL
p1_life_raw	LINE
p2_life_raw	LINE
p1_lives	LABEL
p2_lives	LABEL
p1_score_raw	LINE
p2_score_raw	LINE
p1_score_mirror	NONE
p2_score_mirror	NONE
p1_current_combo	LINE
p2_current_combo	LINE
p1_max_combo	LABEL
p2_max_combo	LABEL
p1_spell_attacks	LABEL
p2_spell_attacks	LABEL
p1_boss_attacks	LABEL
p2_boss_attacks	LABEL
p1_boss_reversals	LABEL
p2_boss_reversals	LABEL
p1_spell_points	LINE
p2_spell_points	LINE
p1_spell_points_mirror	INTERNAL
p2_spell_points_mirror	INTERNAL
p1_gauge	LINE
p2_gauge	LINE
clear_life_bonus	LABEL
clear_max_combo_bonus	LABEL
clear_spell_bonus	LABEL
clear_boss_bonus	LABEL
clear_reversal_bonus	LABEL
clear_lives_bonus	LABEL
clear_total	LABEL
p1_cpu_dodge_mode	BAND
p2_cpu_dodge_mode	BAND
p1_cpu_quick_timer	LINE
p2_cpu_quick_timer	LINE
p1_cpu_stand_timer	LINE
p2_cpu_stand_timer	LINE
p1_zero_hit_timer	LINE
p2_zero_hit_timer	LINE
p1_combo_gauge_raw	NONE
p2_combo_gauge_raw	NONE
p1_enemy_total	LINE
p2_enemy_total	LINE
p1_enemy_fairy	LINE
p2_enemy_fairy	LINE
p1_enemy_boss	LINE
p2_enemy_boss	LINE
p1_enemy_charge	LINE
p2_enemy_charge	LINE
p1_bullet_fairy	LINE
p2_bullet_fairy	LINE
p1_bullet_rival	LINE
p2_bullet_rival	LINE
internal_rank	LINE
rank_interval	LABEL
rank_max	LABEL
p1_charge	LINE
p2_charge	LINE
p1_card_attack_level	LINE
p2_card_attack_level	LINE
p1_boss_card_attack_level	LINE
p2_boss_card_attack_level	LINE
p1_boss_type	INTERNAL
p2_boss_type	INTERNAL
p1_boss_sub	INTERNAL
p2_boss_sub	INTERNAL
p1_boss_depth	INTERNAL
p2_boss_depth	INTERNAL
p1_boss_hp	LINE
p2_boss_hp	LINE
p1_ex_active	LINE
p2_ex_active	LINE
p1_ex_triggered	LINE
p2_ex_triggered	LINE
p1_cpu_quick_timer_cur	LINE
p2_cpu_quick_timer_cur	LINE
p1_cpu_stand_timer_cur	LINE
p2_cpu_stand_timer_cur	LINE
p1_combo_gauge_cur	LINE
p2_combo_gauge_cur	LINE
lily_counter	LINE
p1_pos_x	INTERNAL
p2_pos_x	INTERNAL
p1_pos_y	INTERNAL
p2_pos_y	INTERNAL
p1_input_replay	INTERNAL
p2_input_replay	INTERNAL
p1_white_bullet_points	INTERNAL
p2_white_bullet_points	INTERNAL
p1_ghost_points	INTERNAL
p2_ghost_points	INTERNAL
p1_ex_points	INTERNAL
p2_ex_points	INTERNAL
p1_boss_pos_x	INTERNAL
p2_boss_pos_x	INTERNAL
p1_boss_pos_y	INTERNAL
p2_boss_pos_y	INTERNAL
p1_cpu_charge_instruction	LINE
p2_cpu_charge_instruction	LINE
rng_state	INTERNAL
p1_hit_kind	INTERNAL
p1_hit_obj	INTERNAL
p1_hit_x	INTERNAL
p1_hit_y	INTERNAL
p2_hit_kind	INTERNAL
p2_hit_obj	INTERNAL
p2_hit_x	INTERNAL
p2_hit_y	INTERNAL
hit_damage_base	LINE
coord_ring_state	INTERNAL
os_interrupt_time_lo	INTERNAL
os_tick_count_lo	INTERNAL
p1_hit_obj_ptr	INTERNAL
p2_hit_obj_ptr	INTERNAL
p1_hit_laser_x	INTERNAL
p1_hit_laser_y	INTERNAL
p2_hit_laser_x	INTERNAL
p2_hit_laser_y	INTERNAL
p1_hit_laser_angle	INTERNAL
p2_hit_laser_angle	INTERNAL
p1_hit_elem_radius	INTERNAL
p2_hit_elem_radius	INTERNAL
p1_player_state	NONE
p2_player_state	NONE
p1_invincible_timer	LINE
p2_invincible_timer	LINE
p1_bullet_mgr	INTERNAL
p2_bullet_mgr	INTERNAL
p1_speed_mult	INTERNAL
p2_speed_mult	INTERNAL
p1_hit_list_count	INTERNAL
p2_hit_list_count	INTERNAL
p1_game_flags	INTERNAL
p2_game_flags	INTERNAL
global_state	INTERNAL
p1_move_dir_angle	INTERNAL
p2_move_dir_angle	INTERNAL
p1_cpu_dir_lock	NONE
p2_cpu_dir_lock	NONE
p1_cpu_prev_dir	NONE
p2_cpu_prev_dir	NONE
p1_cpu_dir_hist0	NONE
p1_cpu_dir_hist1	NONE
p1_cpu_dir_hist2	NONE
p1_cpu_dir_hist3	NONE
p1_cpu_dir_hist4	NONE
p1_cpu_dir_hist5	NONE
p1_cpu_dir_hist6	NONE
p1_cpu_dir_hist7	NONE
p2_cpu_dir_hist0	NONE
p2_cpu_dir_hist1	NONE
p2_cpu_dir_hist2	NONE
p2_cpu_dir_hist3	NONE
p2_cpu_dir_hist4	NONE
p2_cpu_dir_hist5	NONE
p2_cpu_dir_hist6	NONE
p2_cpu_dir_hist7	NONE
p1_cpu_dodge_dir_hist0	NONE
p1_cpu_dodge_dir_hist1	NONE
p1_cpu_dodge_dir_hist2	NONE
p1_cpu_dodge_dir_hist3	NONE
p1_cpu_dodge_dir_hist4	NONE
p1_cpu_dodge_dir_hist5	NONE
p1_cpu_dodge_dir_hist6	NONE
p1_cpu_dodge_dir_hist7	NONE
p2_cpu_dodge_dir_hist0	NONE
p2_cpu_dodge_dir_hist1	NONE
p2_cpu_dodge_dir_hist2	NONE
p2_cpu_dodge_dir_hist3	NONE
p2_cpu_dodge_dir_hist4	NONE
p2_cpu_dodge_dir_hist5	NONE
p2_cpu_dodge_dir_hist6	NONE
p2_cpu_dodge_dir_hist7	NONE
p1_item0_kind	NONE
p1_item0_x	NONE
p1_item0_y	NONE
p1_item0_valid	NONE
p1_item1_kind	NONE
p1_item1_x	NONE
p1_item1_y	NONE
p1_item1_valid	NONE
p1_item2_kind	NONE
p1_item2_x	NONE
p1_item2_y	NONE
p1_item2_valid	NONE
p1_item3_kind	NONE
p1_item3_x	NONE
p1_item3_y	NONE
p1_item3_valid	NONE
p2_item0_kind	NONE
p2_item0_x	NONE
p2_item0_y	NONE
p2_item0_valid	NONE
p2_item1_kind	NONE
p2_item1_x	NONE
p2_item1_y	NONE
p2_item1_valid	NONE
p2_item2_kind	NONE
p2_item2_x	NONE
p2_item2_y	NONE
p2_item2_valid	NONE
p2_item3_kind	NONE
p2_item3_x	NONE
p2_item3_y	NONE
p2_item3_valid	NONE
p1_cpu_target_x	NONE
p2_cpu_target_x	NONE
p1_cpu_target_y	NONE
p2_cpu_target_y	NONE
p1_enemy_prio_x	NONE
p1_enemy_prio_y	NONE
p1_enemy_prio_flags	NONE
p2_enemy_prio_x	NONE
p2_enemy_prio_y	NONE
p2_enemy_prio_flags	NONE
p1_enemy_first_x	NONE
p1_enemy_first_y	NONE
p1_enemy_first_flags	NONE
p2_enemy_first_x	NONE
p2_enemy_first_y	NONE
p2_enemy_first_flags	NONE
p1_slow_mult_x	NONE
p2_slow_mult_x	NONE
p1_slow_mult_y	NONE
p2_slow_mult_y	NONE
p1_enemy_sub_mask	NONE
p2_enemy_sub_mask	NONE
seq_end	INTERNAL";

    public const string DisplayScalePacked = @"p1_life_raw	0.5
p1_score_mirror	10.0
p1_score_raw	10.0
p2_life_raw	0.5
p2_score_mirror	10.0
p2_score_raw	10.0";

    public const string SharedFieldsPacked = @"internal_rank
rank_interval
rank_max
lily_counter
hit_damage_base
round_frames
mode
difficulty
stage_index
field_id
battle_bgm_id
pause_used
result_winner
clear_life_bonus
clear_max_combo_bonus
clear_spell_bonus
clear_boss_bonus
clear_reversal_bonus
clear_lives_bonus
clear_total";

    public const int MinRoundFrames = 60;
    public const int CutinFreezeFrames = 32;
    public const int HitDelayTicks = 1;

    public const string DerivedFromClassCountsPacked = @"c2_count
c3_count
boss_count";

    public const string FinalSuffixPacked = @"_max_combo";

    public const string CumulativeSuffixPacked = @"_spell_attacks
_boss_attacks
_boss_reversals";

    public const int CoordBaseP1Bullet = 0;
    public const int CoordBaseP1Enemy = 537;
    public const int CoordBaseP2Bullet = 665;
    public const int CoordBaseP2Enemy = 1202;
    public const int CoordBaseP1Laser = 1330;
    public const int CoordBaseP2Laser = 1378;
    public const int CoordBaseEx = 1426;
    public const int CoordBaseP1Shot = 1682;
    public const int CoordBaseP2Shot = 1810;
    public const int CoordBulletSlots = 537;
    public const int CoordEnemySlots = 128;
    public const int CoordLaserSlots = 48;
    public const int CoordLaserTotal = 96;
    public const int CoordExSlots = 256;
    public const int CoordShotSlots = 128;
    public const int CoordShotTotal = 256;
    public const int CoordSlots = 1938;
    public const int CoordHitlistSlots = 128;

    public const uint CoordStateAliveMask = 0x7u;
    public const uint CoordBulletStateFree = 0x0u;
    public const uint CoordBulletStateTerm = 0x6u;
    public const uint CoordLaserStateFree = 0x0u;
    public const uint CoordExStateFree = 0x0u;
    public const uint CoordShotStateFree = 0x0u;
    public const uint CoordShotStateVanish = 0x2u;
    public const uint CoordLaserPhaseMask = 0xFFu;
    public const uint CoordLaserPhase0 = 0x0u;
    public const uint CoordLaserPhase1 = 0x1u;
    public const uint CoordLaserPhase2 = 0x2u;
    public const uint CoordEnemyLilyBit = 0x2000u;
    public const uint CoordEnemyGhostMask = 0x1C0u;
    public const uint CoordEnemyBossMask = 0xC00u;
    public const uint CoordEnemyFairyZeroMask = 0x4DC0u;
    public const uint CoordBulletStateVanish = 5u;

    public const string EnemyClassFairy = "fairy";
    public const string EnemyClassGhost = "ghost";
    public const string EnemyClassLily = "lily";
    public const string EnemyClassBoss = "boss";
    public const string EnemyClassC2C3 = "c2c3";
    public const string EnemyClassOther = "other";

    public const double LaserHalfFactor = 0.25;

    public const int PreMaxTicks = 600;

    public const double EnemyHitboxDiv = 3.0;

    public const string EnemyHitboxRawPacked = @"boss	48.0	32.0	48.0	32.0
fairy	24.0	24.0	36.0	36.0
ghost	24.0	24.0	24.0	24.0
lily	36.0	36.0	36.0	36.0";

    public const string EnemyHitboxNonePacked = @"c2c3
other";

    public const string EnemyKindIdxFairyRawPacked = @"0	24.0	24.0
1	28.0	28.0
2	32.0	32.0
3	26.0	36.0";

    public const string EnemyKindIdxFairyLeadMinPacked = @"3";

    public const string EnemyKindIdxJaPacked = @"0	青妖精
1	赤妖精
2	緑妖精
3	大妖精";

    public const string CoordFlagsColumn = "coord_flags";
    public const uint CoordFlagEnemyKindIdx = 0x400u;
    public const uint CoordFlagEnemyKindClash = 0x800u;
    public const int CoordEnemyKindIdxShift = 16;
    public const uint CoordEnemyKindIdxMask = 0xFFu;
    public const uint CoordEnemyKindWordMask = 0x7FFFu;
    public const uint CoordEnemyFairyLeadBit = 0x200u;

    public const string BulletSpritesPacked = @"0	白弾	-1
1	小弾	-1
2	米粒弾	-1
3	小弾	-1
5	氷弾	5
6	鱗弾	-1
7	中弾	-1
8	蝶弾	10
9	刀弾	3
11	札弾	0
12	星弾	1
13	星弾	1
16	銃弾	4
18	楕円弾	12
19	音符弾	-1
20	ナイフ弾	2
21	銭弾	12
22	卒塔婆弾	13";

    public const string BulletOriginSitesPacked = @"1	enemy
2	enemy
3	white_bullet
4	ghost_penalty
5	bullet_item
6	ex
7	ex
8	ex
9	enemy
10	enemy
11	enemy
12	enemy
13	enemy
14	ex
15	ex
16	ex
17	ex
18	ex";

    public const string BulletOriginLabelsPacked = @"bullet_item	弾アイテム・送り返し
enemy	敵の弾
ex	Ex アタック
ghost_penalty	幽霊のペナルティ
white_bullet	おくりもの";

    public const string BulletOriginForcedClassPacked = @"9	c2c3	2
10	c2c3	2
11	c2c3	3
12	boss	
13	boss	";

    public const uint CoordEnemyLauncherBit = 0x4000u;
    public const int CharSakuya = 2;

    public const string EnemyJaPacked = @"boss	ボス
c2c3	C2/C3
fairy	妖精
ghost	幽霊
lily	リリーホワイト
other	その他";

    public const string CardLevelJaPacked = @"2	C2
3	C3";

    public const uint EnemyCatC2 = 1u;
    public const uint EnemyCatC3 = 2u;
    public const int CoordEnemyCatShift = 10;
    public const uint CoordEnemyCatMask = 0x3u;

    public const string CardLevelDegenerateCharsPacked = @"4
5
8
9
11";

    public const int EnemyClassCountsBits = 8;
    public const int EnemyClassCountsFields = 3;

    public const double ReuseJump = 10.0;

    public const int CoordOriginSiteShift = 3;
    public const uint CoordOriginSiteMask = 0x1Fu;
    public const int CoordOriginEnemyShift = 8;
    public const uint CoordOriginEnemyMask = 0xFFu;

    public const int CoordLaserOriginSubShift = 3;
    public const uint CoordLaserOriginSubMask = 0x1Fu;
    public const uint CoordLaserOriginSubCap = 30u;
    public const int CoordLaserOriginEnemyShift = 8;
    public const uint CoordLaserOriginEnemyMask = 0xFFu;
    public const uint CoordFlagLaserOrigin = 0x4000u;

    public const int PlayerStateNormal = 0;

    public const string InvincibleReasonJaPacked = @"both	やられ／被弾後 ＋ 無敵タイマー
state	やられ／被弾後（動けない）
timer	無敵タイマー（動ける）";

    public const string InvincibleLimitNote = @"この帯は pN_player_state と pN_invincible_timer が言う無敵だけ（Extra の CPU に効く『真の無敵』は別の仕組みで、記録した語に出ない）";
    public const uint CoordShotKindSpriteMask = 0xFFu;
    public const int CoordShotKindBehavShift = 8;
    public const uint CoordShotKindBehavMask = 0xFu;
    public const int CoordShotKindEntryShift = 12;
    public const uint CoordShotKindEntryMask = 0xFFFu;

    public const string ShotC1EntryPacked = @"0	1424
1	1424
2	1424
3	1424
4	1424
5	1424
7	1424
8	1424
9	1592
10	1424
11	1424
12	1424
13	1424
14	1432";

    public const string ShotC1JaTrue = "C1";
    public const string ShotC1JaFalse = "通常ショット";
    public const string ShotC1UnknownJa = "通常か C1 か不明";
    public const string ShotLegendJa = "ショット";
    public const string ShotSizeRangeJa = "窓の中で変わる";

    public const double ShotC1LDrop = 0.09;

    public const string ShotVanishNote = @"命中後（消滅演出）のショットは描いていない";

    public const string ClearRingKindsPacked = @"c2	0x00403C30	0.0	4.0	48
c3	0x00403C90	0.0	4.0	64
c4	0x00403CF0	0.0	3.5	128
hit	0x0041E88C	16.0	20.0	8";

    public const string ClearRingJaPacked = @"c2	弾消しリング（C2）
c3	弾消しリング（C3）
c4	弾消しリング（C4）
hit	弾消しリング（被弾）";
    public const string ClearRingLegendJa = "弾消しリング";
    public const string ClearRingSourcesJa = "C2~4, 被弾で発生";

    public const string RingPreNote = @"発動が窓の頭より前（シークバーに点は出ない）";
    public const string QuickLevelNote = @"残ゲージが閾値の直前（±1）なので、1 段上だった可能性がある";
    public const string QuickDowngradeNote = @"相手にボスを送っている状態の 4 本なので、1 本残して C3 に格下げ";
    public const string CardNoBossWordNote = @"ボスアタックの回数が読めないので、C4 とクイックの C3 を分けられない";

    public const string HitRingShortJa = "被弾弾消し";
    public const string RecoveryShortJa = "ゲージ回復";
    public const string RecoveryLostShortJa = "喰らいチャージ";
    public const string RecoveryWithRingJa = "ゲージ回復＆弾消し";
    public const string RingOnlyJa = "弾消しのみ";
    public const string CardNoteRecoveryLostJa = "喰らい";

    public const string RecoveryCancelNote = @"被弾してから復帰するまでにカードアタックを撃つと、被弾ぶんのゲージ回復が消える（喰らいチャージ）";
    public const string RecoveryOutsideNote = @"回復の予定時刻が窓の外（記録がここで終わっている）";

    public const int RecoveryValidTicks = 61;
    public const int RecoveryMatchTol = 30;
    public const double RecoveryMinRise = 20.0;

    public const double GaugeDropEps = 1e-06;

    public const string EnemyBlastScalePacked = @"0	5.0
1	5.5
2	6.0";
    public const double EnemyBlastScaleOther = 6.25;

    public const string EnemyBlastLifePacked = @"0	8
1	9
2	10
3	11";
    public const double EnemyBlastR0 = 32.0;
    public const int EnemyBlastWait = 4;
    public const string EnemyBlastJa = "撃破時の爆風";

    public const string SpiritShapeCircle = "circle";
    public const string SpiritShapeCircleUp = "circle_up";
    public const string SpiritShapeBandV = "band_v";
    public const string SpiritShapeBandH = "band_h";
    public const string SpiritShapeCross = "cross";
    public const string SpiritShapeFan = "fan";
    public const string SpiritShapeConeUp = "cone_up";
    public const string SpiritShapeLens = "lens";
    public const string SpiritShapeFlower = "flower";
    public const string SpiritShapeStar = "star";
    public const string SpiritAimFixed = "fixed";
    public const string SpiritAimMoveBack = "move_back";
    public const string SpiritAimSpin = "spin";
    public const string SpiritAimVx = "vx";

    public const string SpiritFieldPacked = @"0	circle		0	
1	cone_up		30	
2	fan	move_back	0	
3	circle		0	
4	lens	move_back	0	0.0
5	circle		0	
6	band_v		0	
7	circle_up		0	
8	lens	fixed	0	0.0
9	flower	spin	0	0.0
10	fan	fixed	0	-1.5707963267948966
11	star	vx	0	-1.5707963267948966
12	circle		0	
13	circle		0	
14	band_h		0	
15	cross		0	";

    public const string SpiritParamsPacked = @"0	r0	16.0
0	cap	96.0
0	dr	4.0
1	r0	0.01
1	cap	0.08
1	dr	0.001
1	span	448.0
1	min_half	14.0
1	y_gate	32.0
1	delay	30.0
2	r0	0.0
2	cap	1.5707963267948966
2	dr	0.05235987755982988
2	radius	160.0
2	lerp	0.25
3	r0	16.0
3	cap	144.0
3	dr	0.7
4	r0	16.0
4	cap	112.0
4	dr	8.0
4	k	0.5
4	lerp	0.125
5	r0	16.0
5	cap	48.0
5	dr	8.0
6	r0	0.0
6	cap	16.0
6	dr	0.5
7	r0	16.0
7	cap	56.0
7	dr	4.0
7	offset	1.5
8	r0	16.0
8	cap	320.0
8	dr	8.0
8	k	0.866025
9	r0	32.0
9	cap	48.0
9	dr	4.0
9	petal_r	24.0
9	petals	6.0
9	spin	0.05235987755982988
10	r0	0.0
10	cap	1.5707963267948966
10	dr	0.031415926535897934
10	radius	128.0
11	r0	16.0
11	cap	128.0
11	dr	6.0
11	points	5.0
11	inner	1.618034
12	r0	640.0
12	cap	0.0
12	dr	-32.0
13	r0	16.0
13	cap	96.0
13	dr	16.0
14	r0	0.0
14	cap	24.0
14	dr	0.5
15	r0	0.0
15	cap	8.0
15	dr	0.5";

    public const string SpiritDrawKeysPacked = @"band_h	
band_v	
circle	
circle_up	
cone_up	span,min_half,y_gate
cross	
fan	radius
flower	petal_r,petals
lens	k
star	points,inner";

    public const string SpiritRampPacked = @"0	16.0 20.0 24.0 28.0 32.0 36.0 40.0 44.0 48.0 52.0 56.0 60.0 64.0 68.0 72.0 76.0 80.0 84.0 88.0 92.0 96.0
1	0.009999999776482582 0.010999999940395355 0.012000000104308128 0.013000000268220901 0.014000000432133675 0.015000000596046448 0.01600000075995922 0.017000000923871994 0.018000001087784767 0.01900000125169754 0.020000001415610313 0.021000001579523087 0.02200000174343586 0.023000001907348633 0.024000002071261406 0.02500000223517418 0.026000002399086952 0.027000002562999725 0.0280000027269125 0.02900000289082527 0.030000003054738045 0.031000003218650818 0.03200000151991844 0.032999999821186066 0.03399999812245369 0.034999996423721313 0.03599999472498894 0.03699999302625656 0.037999991327524185 0.03899998962879181 0.03999998793005943 0.04099998623132706 0.04199998453259468 0.042999982833862305 0.04399998113512993 0.04499997943639755 0.045999977737665176 0.0469999760389328 0.047999974340200424 0.04899997264146805 0.04999997094273567 0.050999969244003296 0.05199996754527092 0.052999965846538544 0.05399996414780617 0.05499996244907379 0.055999960750341415 0.05699995905160904 0.05799995735287666 0.05899995565414429 0.05999995395541191 0.060999952256679535 0.06199995055794716 0.06299994885921478 0.0639999508857727 0.06499995291233063 0.06599995493888855 0.06699995696544647 0.0679999589920044 0.06899996101856232 0.06999996304512024 0.07099996507167816 0.07199996709823608 0.072999969124794 0.07399997115135193 0.07499997317790985 0.07599997520446777 0.0769999772310257 0.07799997925758362 0.07899998128414154 0.07999998331069946 0.08099998533725739
2	0.0 0.05235987901687622 0.10471975803375244 0.15707963705062866 0.20943951606750488 0.2617993950843811 0.3141592741012573 0.36651915311813354 0.41887903213500977 0.471238911151886 0.5235987901687622 0.5759586691856384 0.6283185482025146 0.6806784272193909 0.7330383062362671 0.7853981852531433 0.8377580642700195 0.8901179432868958 0.942477822303772 0.9948377013206482 1.0471975803375244 1.0995573997497559 1.1519172191619873 1.2042770385742188 1.2566368579864502 1.3089966773986816 1.361356496810913 1.4137163162231445 1.466076135635376 1.5184359550476074 1.5707957744598389 1.6231555938720703
3	16.0 16.700000762939453 17.400001525878906 18.10000228881836 18.800003051757812 19.500003814697266 20.20000457763672 20.900005340576172 21.600006103515625 22.300006866455078 23.00000762939453 23.700008392333984 24.400009155273438 25.10000991821289 25.800010681152344 26.500011444091797 27.20001220703125 27.900012969970703 28.600013732910156 29.30001449584961 30.000015258789062 30.700016021728516 31.40001678466797 32.10001754760742 32.800018310546875 33.50001907348633 34.20001983642578 34.900020599365234 35.60002136230469 36.30002212524414 37.000022888183594 37.70002365112305 38.4000244140625 39.10002517700195 39.800025939941406 40.50002670288086 41.20002746582031 41.900028228759766 42.60002899169922 43.30002975463867 44.000030517578125 44.70003128051758 45.40003204345703 46.100032806396484 46.80003356933594 47.50003433227539 48.200035095214844 48.9000358581543 49.60003662109375 50.3000373840332 51.000038146972656 51.70003890991211 52.40003967285156 53.100040435791016 53.80004119873047 54.50004196166992 55.200042724609375 55.90004348754883 56.60004425048828 57.300045013427734 58.00004577636719 58.70004653930664 59.400047302246094 60.10004806518555 60.800048828125 61.50004959106445 62.200050354003906 62.90005111694336 63.60005187988281 64.300048828125 65.00004577636719 65.70004272460938 66.40003967285156 67.10003662109375 67.80003356933594 68.50003051757812 69.20002746582031 69.9000244140625 70.60002136230469 71.30001831054688 72.00001525878906 72.70001220703125 73.40000915527344 74.10000610351562 74.80000305175781 75.5 76.19999694824219 76.89999389648438 77.59999084472656 78.29998779296875 78.99998474121094 79.69998168945312 80.39997863769531 81.0999755859375 81.79997253417969 82.49996948242188 83.19996643066406 83.89996337890625 84.59996032714844 85.29995727539062 85.99995422363281 86.699951171875 87.39994812011719 88.09994506835938 88.79994201660156 89.49993896484375 90.19993591308594 90.89993286132812 91.59992980957031 92.2999267578125 92.99992370605469 93.69992065429688 94.39991760253906 95.09991455078125 95.79991149902344 96.49990844726562 97.19990539550781 97.89990234375 98.59989929199219 99.29989624023438 99.99989318847656 100.69989013671875 101.39988708496094 102.09988403320312 102.79988098144531 103.4998779296875 104.19987487792969 104.89987182617188 105.59986877441406 106.29986572265625 106.99986267089844 107.69985961914062 108.39985656738281 109.099853515625 109.79985046386719 110.49984741210938 111.19984436035156 111.89984130859375 112.59983825683594 113.29983520507812 113.99983215332031 114.6998291015625 115.39982604980469 116.09982299804688 116.79981994628906 117.49981689453125 118.19981384277344 118.89981079101562 119.59980773925781 120.2998046875 120.99980163574219 121.69979858398438 122.39979553222656 123.09979248046875 123.79978942871094 124.49978637695312 125.19978332519531 125.8997802734375 126.59977722167969 127.29977416992188 127.99977111816406 128.69976806640625 129.39976501464844 130.09976196289062 130.7997589111328 131.499755859375 132.1997528076172 132.89974975585938 133.59974670410156 134.29974365234375 134.99974060058594 135.69973754882812 136.3997344970703 137.0997314453125 137.7997283935547 138.49972534179688 139.19972229003906 139.89971923828125 140.59971618652344 141.29971313476562 141.9997100830078 142.69970703125 143.3997039794922 144.09970092773438
4	16.0 24.0 32.0 40.0 48.0 56.0 64.0 72.0 80.0 88.0 96.0 104.0 112.0
5	16.0 24.0 32.0 40.0 48.0
6	0.0 0.5 1.0 1.5 2.0 2.5 3.0 3.5 4.0 4.5 5.0 5.5 6.0 6.5 7.0 7.5 8.0 8.5 9.0 9.5 10.0 10.5 11.0 11.5 12.0 12.5 13.0 13.5 14.0 14.5 15.0 15.5 16.0
7	16.0 20.0 24.0 28.0 32.0 36.0 40.0 44.0 48.0 52.0 56.0
8	16.0 24.0 32.0 40.0 48.0 56.0 64.0 72.0 80.0 88.0 96.0 104.0 112.0 120.0 128.0 136.0 144.0 152.0 160.0 168.0 176.0 184.0 192.0 200.0 208.0 216.0 224.0 232.0 240.0 248.0 256.0 264.0 272.0 280.0 288.0 296.0 304.0 312.0 320.0
9	32.0 36.0 40.0 44.0 48.0
10	0.0 0.03141592815518379 0.06283185631036758 0.09424778819084167 0.12566371262073517 0.15707963705062866 0.18849556148052216 0.21991148591041565 0.25132742524147034 0.282743364572525 0.3141593039035797 0.3455752432346344 0.3769911825656891 0.4084071218967438 0.43982306122779846 0.47123900055885315 0.5026549100875854 0.5340708494186401 0.5654867887496948 0.5969027280807495 0.6283186674118042 0.6597346067428589 0.6911505460739136 0.7225664854049683 0.753982424736023 0.7853983640670776 0.8168143033981323 0.848230242729187 0.8796461820602417 0.9110621213912964 0.9424780607223511 0.9738940000534058 1.0053099393844604 1.0367258787155151 1.0681418180465698 1.0995577573776245 1.1309736967086792 1.1623896360397339 1.1938055753707886 1.2252215147018433 1.256637454032898 1.2880533933639526 1.3194693326950073 1.350885272026062 1.3823012113571167 1.4137171506881714 1.445133090019226 1.4765490293502808 1.5079649686813354 1.5393809080123901 1.5707968473434448
11	16.0 22.0 28.0 34.0 40.0 46.0 52.0 58.0 64.0 70.0 76.0 82.0 88.0 94.0 100.0 106.0 112.0 118.0 124.0 130.0
12	640.0 608.0 576.0 544.0 512.0 480.0 448.0 416.0 384.0 352.0 320.0 288.0 256.0 224.0 192.0 160.0 128.0 96.0 64.0 32.0 0.0
13	16.0 32.0 48.0 64.0 80.0 96.0
14	0.0 0.5 1.0 1.5 2.0 2.5 3.0 3.5 4.0 4.5 5.0 5.5 6.0 6.5 7.0 7.5 8.0 8.5 9.0 9.5 10.0 10.5 11.0 11.5 12.0 12.5 13.0 13.5 14.0 14.5 15.0 15.5 16.0 16.5 17.0 17.5 18.0 18.5 19.0 19.5 20.0 20.5 21.0 21.5 22.0 22.5 23.0 23.5 24.0
15	0.0 0.5 1.0 1.5 2.0 2.5 3.0 3.5 4.0 4.5 5.0 5.5 6.0 6.5 7.0 7.5 8.0";

    public const string SpiritAimVxPacked = @"11	-1	-2.094395	0.02
11	0	-1.570796	0.05
11	1	-1.047198	0.02";

    public const int SpiritSweepSteps = 24;
    public const double MoveAngleHitLo = 0.0;
    public const double MoveAngleHitHi = 3.141592653589793;
    public const string SpiritFieldJa = "吸霊範囲";
    public const string SpiritAngleUnknownSuffix = "・向き不明";

    public const string ExUpdateFpPacked = @"0x00441100	0x00	reimu
0x00441A70	0x02	marisa
0x00442750	0x03	sakuya
0x00442BD0	0x04	sakuya_chain
0x004435E0	0x0D	youmu
0x00443DF0	0x0E	reisen
0x00445190	0x05	cirno
0x00445740	0x0F	lyrica
0x00446060	0x06	mystia
0x004464A0	0x07	
0x00446920	0x08	
0x00446EE0	0x0B	
0x004471B0	0x0C	
0x00447890	0x10	tewi
0x00448490	0x11	yuuka
0x004491E0	0x12	aya
0x0044A330	0x13	medicine_mist
0x0044ADE0	0x14	komachi
0x0044B500	0x15	eiki_trial
0x0044BE40	0x19	merlin
0x0044C4C0	0x1A	lunasa";

    public const string ExHitboxPacked = @"0x00441100	circle	18.0									
0x00441A70											
0x00442750											
0x00442BD0	circle	12.0								0 7 13 19 25	
0x004435E0	circle	14.0									
0x00443DF0	circle				reisen	2	30	still			
0x00445190	aabb		6.0	32.0				not_moving_x			
0x00445740											
0x00446060											
0x004464A0											
0x00446920											
0x00446EE0											
0x004471B0											
0x00447890	circle	13.0									
0x00448490	circle										0:28.0,1:28.0,2:18.0,3:18.0
0x004491E0	circle	12.0							next_tick		
0x0044A330	circle	64.0				21	299	effect_only			
0x0044ADE0											
0x0044B500	circle	32.0						effect_only			
0x0044BE40											
0x0044C4C0											";

    public const string ExNameJaPacked = @"aya	天狗烈風弾
cirno	アイシクルフォール
eiki_trial	弾幕裁判
komachi	故人の縁
lunasa	スローサウンド
lyrica	ファントムノイズ
marisa	アースライトレイ
medicine_mist	スウィートポイズン
merlin	トランペットソウル
mystia	バードウォッチング
reimu	陰陽玉
reisen	メタフィジカルマインド
sakuya	アナザーマーダー
sakuya_chain	アナザーマーダー
tewi	兎玉
youmu	未断の魂
yuuka	幻想春花";

    public const string ExColorsPacked = @"eiki_trial	#e3a3ff
medicine_mist	#8fe36b";

    public const string ExNotePacked = @"eiki_trial	通過した弾と同じ方向に速度を加えた卒塔婆弾を射出する
medicine_mist	重なった枚数だけ移動速度が 0.4^N";

    public const string ExHitboxArmPacked = @"0x00	21
0x04	30
0x05	13
0x0D	21
0x10	21
0x11	21
0x12	21
0x13	21";

    public const string ExCardLevelPacked = @"0x07	2
0x08	3
0x09	3";

    public const int ExTimerInnerDelta = -1;

    public const string ExReisenRadiusPacked = @"2	4.177777777777772
3	6.2
4	8.177777777777782
5	10.111111111111109
6	12.0
7	13.844444444444447
8	15.64444444444444
9	17.400000000000002
10	19.111111111111114
11	20.77777777777778
12	22.400000000000002
13	23.977777777777774
14	25.511111111111116
15	27.0
16	28.444444444444443
17	29.84444444444445
18	31.200000000000003
19	32.51111111111111
20	33.777777777777786
21	35.0
22	36.17777777777777
23	37.31111111111112
24	38.400000000000006
25	39.44444444444445
26	40.44444444444445
27	41.400000000000006
28	42.31111111111112
29	43.17777777777778
30	44.0";

    public const string ExParentTypesPacked = @"0x03";

    public const uint ExVariantMask = 0xFFFFu;

    public const int BossTypeBoss = 3;

    public const uint ExReisenUpdateFp = 0x00443DF0u;
    public const double FieldX0 = -136.0;
    public const double FieldX1 = 136.0;
    public const double FieldY0 = 16.0;
    public const double FieldY1 = 432.0;

    public const uint HitlistValid = 0x80000000u;

    public const int CoordBlastSlots = 32;
    public const uint CoordBlastPresent = 0x1u;
    public const int CoordBlastSideShift = 1;
    public const uint CoordBlastSideMask = 0x1u;
    public const uint CoordBlastPosOk = 0x4u;
    public const int CoordBlastEnemyShift = 8;
    public const uint CoordBlastEnemyMask = 0xFFu;
    public const int CoordBlastIdxShift = 16;
    public const uint CoordBlastIdxMask = 0xFFu;
    public const uint CoordFlagEnemyBlast = 0x1000u;

    public const string BlastSuffixesPacked = @"_x
_y
_kind
_word";

    public const string BlastBasesPacked = @"blast0
blast1
blast2
blast3
blast4
blast5
blast6
blast7
blast8
blast9
blast10
blast11
blast12
blast13
blast14
blast15
blast16
blast17
blast18
blast19
blast20
blast21
blast22
blast23
blast24
blast25
blast26
blast27
blast28
blast29
blast30
blast31";

    public const int CoordBlastEnemyBias = 1;

    public const string EnemyBlastSizeJa = @"サイズは敵依存";
    public const string EnemyBlastRecordNote = @"ゲームが作った爆風をそのまま描いている（敵の種類で絞っていない）";
    public const string EnemyBlastInferNote = @"この窓は爆風を記録していない（座標リング v7 以前）ので妖精の撃破からだけ描いている";
    public const string EnemyBlastNoKindNote = @"この窓は妖精の色を記録していない（座標リング v7 以前）ので爆風を描いていない";
    public const string EnemyBlastEraseNote = @"消せるのは白弾だけ（弾消しリングは種類を問わず消す）";
    public const uint InputReplayBomb = 0x2u;
    public const uint InputReplaySlow = 0x4u;
    public const uint InputReplayUp = 0x10u;
    public const uint InputReplayDown = 0x20u;
    public const uint InputReplayLeft = 0x40u;
    public const uint InputReplayRight = 0x80u;
    public const uint InputReplayDirMask = 0xF0u;

    public const string MoveDirAnglePacked = @"1	-1.5707963267948966
2	1.5707963267948966
3	3.141592653589793
4	0.0
5	-2.356194490192345
6	-0.7853981633974483
7	2.356194490192345
8	0.7853981633974483";

    public const string MoveDirVxSignPacked = @"0	0
1	0
2	0
3	-1
4	1
5	-1
6	1
7	-1
8	1";

    public const double MoveAngleInit = -1.5707963267948966;

    public const string MoveAngleOptionsPacked = @"-2.356194490192345
-1.5707963267948966
-0.7853981633974483
0.0
0.7853981633974483
1.5707963267948966
2.356194490192345
3.141592653589793";

    public const int PlayerStateDown = 4;

    public const string MoveSkipStatesPacked = @"1
2
4
5";

    public const int RoundHeadFrames = 1;
    public const uint HitValid = 0x80000000u;
    public const int HitTypeShift = 16;
    public const uint HitTypeMask = 0x3u;

    public const string HitTypeJaPacked = @"0	弾
1	Exサークル
2	レーザー
3	体当たり/Exボックス";

    public const string CandidateWhyJaPacked = @"bullet_no_ptr	当たった弾を指すポインタが取れない
enemy_not_found	自機と重なっていた敵が無い（妖精は判定がいちばん大きい場合で見ている）
ex_not_found	自機を含む Ex の判定が見つからない
laser_not_found	自機の近くに致死のレーザーが無い
no_ex_slots	この窓は Ex 枠を記録していない（古い座標リング）ので Ex は候補にできない
no_player_side	自機の位置か一辺が取れない
resolved	
unknown_type	被弾の種別が分からない（詰みクイックなど被弾でない窓を含む）";

    public const string EnemyHowJaPacked = @"enemy	その tick に自機と重なっていた敵（大きさは静的表。幾何の当てはめ）
enemy_ranged	同上。★妖精は判定の大きさに幅があり、最小と最大で候補が変わる（1 体に絞れない）";

    public const string EnemyIdentifyHowJaPacked = @"hit座標	当たった判定要素の座標が敵の枠と一致した（ゲームの値）";

    public const string ExHowJaPacked = @"hit座標	当たった判定要素の座標が Ex 枠と一致した（ゲームの値）
hit座標(トレイル)	同上。咲夜は円 5 個で、当たったのはその Ex 自身の過去の位置
hit座標+1tick	同上。ただし文だけ判定が 1 tick 先に置かれる（center=next_tick）
hit座標+1tick(外挿)	同上。その 1 tick 先が凍結で進んでいないので、直前 3 tick から等加速度で外挿した位置と一致した";

    public const uint BulletArrayBase = 0x1A900u;
    public const uint BulletArrayStride = 0x10C4u;

    public const double HitXyTol = 0.001;
    public const double HitAngleTol = 0.0001;

    public const uint HitlistCountMask = 0x1FFu;

    public const string CharHitRadiusPacked = @"0	2.0
1	2.299999952316284
2	2.299999952316284
3	2.299999952316284
4	2.299999952316284
5	2.299999952316284
6	2.299999952316284
7	2.299999952316284
8	2.200000047683716
9	2.200000047683716
10	2.200000047683716
11	2.200000047683716
12	2.200000047683716
13	2.200000047683716
14	2.299999952316284
15	2.299999952316284";
    public const string ScreenOriginPacked = @"1	160.0	16.0
2	480.0	16.0";

    public const string ScreenXyExTypesPacked = @"0x02
0x03";

    public const string BoardXyExTypesPacked = @"0x04";

    public const string ExFlightFramesPacked = @"0x05	41
0x0D	181";
    public const int ExFlightFramesDefault = 91;

    public const double ExJumpTol = 0.6;
    public const double ExBoardXLimit = 178.0;

    public const string ExBossBirthTimerPacked = @"0x00441100	91
0x00443DF0	
0x00445190	41
0x00446EE0	91
0x004471B0	91
0x00447890	91
0x004491E0	91";

    public const string ExSuffixesPacked = @"_timer
_side
_variant";
    public const uint GfTimeStop = 0x1u;
    public const uint GsFreezeP1 = 0x800u;
    public const uint GsFreezeP2 = 0x1000u;
    public const uint FlagPlayersValid = 0x1u;

    public const string FreezeWordFieldsPacked = @"p1_game_flags
p2_game_flags
global_state";

    public const string FreezeKindsPacked = @"both	両陣が停止
cutin	カットイン
round_end	ラウンドの決着
round_over	ラウンド終了
round_start	ラウンド開始
time_stop	%sP 時止め
time_stop_mirror	時止め";

    public const string FreezeMarkPriorityPacked = @"cutin
round_end
round_start";

    public const string MirrorSkipEnemyCatsPacked = @"1
2";

    public const int FreezeMarkTol = 2;
    public const int FreezeWordEdgeTicks = 1;
    public const int RoundOverMinTicks = 2;
    public const int MirrorMinLive = 8;
    public const int MirrorMinFrames = 6;
    public const int SakuyaCharacter = 2;

    public const string FreezeUncertainSuffix = @"・推定";
    public const string FreezeWarnStopSides = @"時止めのフラグが片側にしか立っていない（両側の pN_game_flags が不一致）";
    public const string BossAttackNamesPacked = @"0	3	全方位赤札
0	4	自機狙い変化白札
0	5	陰陽玉ばら撒き
0	6	自機狙い赤札+3way白弾
0	7	全方位交差白弾
1	3	自機狙い青星弾
1	4	ばら撒き不規則黄星弾
1	5	ランダム斜め赤青レーザー
1	6	自機外し緑レーザー
1	7	全方位青緑白弾＆星弾
2	3	ばら撒き壁反射紫
2	4	移動発射青ナイフ
2	5	ハの字型黄ナイフ
2	6	自機狙い空ナイフ+追従設置白弾
3	3	自機狙い青刀弾
3	4	円形空刀弾
3	5	自機狙い三角黄ナイフ
4	3	全方位低速銃弾
4	4	螺旋状高速銃弾
4	5	Ex弾3way
5	3	真下青氷弾連射
5	4	全方位白弾+氷柱
5	5	全方位白弾+氷柱
5	6	全方位交差空氷弾
6	3	全方位赤音符弾
6	4	3way鱗弾+白弾
6	5	3way鱗弾+白弾
6	6	全方位赤ワインダー鱗弾
7	3	自機狙い鳥弾+追従設置白弾/鱗弾
7	4	緑小弾+自機狙い鳥弾
7	5	緑小弾+自機狙い鳥弾
7	6	自機狙い7way鳥弾
8	3	左右交互自機外し米粒弾/白弾
8	4	設置交差中弾
8	5	Ex連射
9	3	全方位交差反射赤黄小弾
9	4	自機外し＆薙ぎ払いワインダー中弾
9	5	全方位赤黄米粒弾/中弾
10	3	全方位高速蝶弾
10	4	自機狙いEx連射
10	5	高速ばら撒き小弾/白弾
11	3	薙ぎ払い中弾（左→右）
11	4	薙ぎ払い中弾（右→左）
11	6	ばら撒き米粒弾/白弾
12	3	3way黄楕円弾+8way黄銭弾
12	4	自機方向楕円弾/銭弾/白弾ばら撒き
12	5	全方位停滞赤楕円弾＋赤銭弾
12	6	全方位銀銭弾+銀白弾
13	3	自機狙い極太レーザー+左右端8wayレーザー
13	4	自機外し8way卒塔婆ワインダー
13	5	高速全方位米弾/白弾
14	3	自機狙い3way青音符弾3セット
14	4	回転白弾+逆回転青音符弾
14	5	自機狙い多重音符弾列
15	3	ばら撒き黄音符弾3セット
15	4	低速ばら撒き弾
15	5	自機狙い変化黄音符弾";

    public const string BossDisplayNamesPacked = @"0	霊符「博麗幻影」
1	魔符「イリュージョンスター」
2	時符「ミステリアスジャック」
3	迷符「半身大悟」
4	散符「栄華之夢（ルナメガロポリス）」
5	凍符「コールドディヴィニティー」
6	騒符「リリカ・ソロライブ」
7	鳥符「ミステリアスソング」
8	兎符「因幡の素兎」
9	幻想「花鳥風月、嘯風弄月」
10	疾風「風神少女」
11	毒符「憂鬱の毒」
12	死神「ヒガンルトゥルー」
13	審判「ラストジャッジメント」
14	騒符「メルラン・ハッピーライブ」
15	騒符「ルナサ・ソロライブ」";

    public const string BossIdleSubsPacked = @"0	2
1	2
2	2
3	2
4	2
5	2
6	2
7	2
8	2
9	2
10	2
11	2
12	2
13	2
14	2
15	2";
    public const string SideLabelFmt = @"%dP %s";
    public const string NoteLabelFmt = @"%s(%s)";
    public const string LabelJoin = @"&";
    public const string CardNoteJoin = @"・";

    public const string CardLevelShortJaPacked = @"c2	C2
c3	C3
c4	C4";

    public const string CardSourceNoteJaPacked = @"gauge	
quick	Quick
spell	50万";

    public const string CardSourceJaPacked = @"gauge	カードアタック
quick	クイックカードアタック
spell	50万C3";

    public const string CardSpellNote = @"スペルポイント由来はゲージも弾消しリングも出ない（点だけ）";

    public const string BossShortJaPacked = @"boss	Boss
reversal	Rev";

    public const string BossLevelJaPacked = @"100000	10万
300000	30万
500000	50万";

    public const string BossLevelBySpPacked = @"500000
300000
100000";

    public const int CutinCardTol = 2;

    public const int TimeStopLeadTicks = 40;
    public const int TimeStopLeadTolLo = -1;
    public const int TimeStopLeadTolHi = 1;
    public const int RoundCutValidTicks = 14;

    public const int RoundOverShowTicks = 57;
}
