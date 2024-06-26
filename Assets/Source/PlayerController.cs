using UnityEngine;
using System.Collections.Generic;
using System;
using Source.Features.SceneEditor.Controllers;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using static UnityEngine.Mathf;
using static UnityEngine.Physics;
using static Source.Utils.Utils;
using static EnemyType;

public enum PlayerClass{
    Attacker, 
    Balanced
}

[Serializable]
public class PlayerSettings{
    public float baseSpeed = 20; 
    public float sprintSpeed = 40;
    public float sprintStaminaDrain = 10;
    public float groundAcceleration = 15;
    public float groundDeceleration = 5;
    public float airAcceleration = 2;
    public float airDeceleration = 5;
    public float friction = 50;
    public float gravity = 40;
    public float minJumpForce = 20;
    public float maxJumpForce = 140;
    public float timeToChargeMaxJump = 3;
    public float jumpChargeStaminaDrain = 30;
    public float jumpForwardBoost = 10;
    public float coyoteTime = 0.2f;
    public float jumpBufferTime = 0.3f;
    public float maxStamina = 100;
    public float staminaRecoveryRate = 5;
    public float bulletTimeStaminaDrain = 10;
    public float ballReloadTime = 5;
    public int   maxBallCount = 3;
    
    //hook
    public float hookPullDelay = 0.4f;
    public float hookPullPower = 40;
    public float hookUpPower = 20;
    public float ballDetectRadius = 7;
    
    //Kick
    public float kickHitBoxLength = 5;
    public float kickHitBoxWidth = 5;
    public float kickDuration = 0.2f;
    public float kickCooldown = 0.3f;
    public float kickPower = 100f;
    
    [Header("Balls")]
    public bool haveScope;
    public float ballCollectRadius = 6;
    public float shootCooldown = 0.05f;
    public float maxBallSpeed = 50;
    public float ballGravity = 25;
    public float angularVelocityPower = 2;
    public float angularVelocitySense = 2;
    public float maxAngularVelocity = 100;
    public float angularVelocityDecreaseRate = 1;
    public float findEnemiesRadius = 100;
}

public class PlayerController : MonoBehaviour{
    [Header("Player")]
    [SerializeField] private Transform directionTransform;
    public PlayerClass playerClass;
    
    [SerializeField] private PlayerSettings attackerSettings;
    [SerializeField] private PlayerSettings balancedSettings;
    
    private PlayerSettings _player;
    
    private List<int> _alreadyHitByKick = new();
    private float _kickPerformingTimer;
    private float _kickCooldownTimer;
    
    private ParticleSystem _kickModelParticle;
    private ParticleSystem _kickHitParticles;
    
    private float _currentFriction;
    private float _currentStamina;
    private float _jumpChargeProgress;
    private float _currentSpeed;
    private float _timeSinceGrounded;
    private float _jumpBufferTimer;
    
    private float _playerTimeScale = 1;
    
    private bool _holdingBall;
    private bool _catchedBallOnInput;
    private float _holdingBallTime;
    private float _timeSinceHoldingBall;
    private float _ballReloadCountdown;
    private int   _currentBallCount;
    
    private bool _needToJump;
    
    private Rope _ropePrefab;
    
    private Slider          _staminaSlider;
    private TextMeshProUGUI _ballCounterTextMesh;
    
    private bool _grounded;
    
    private Vector3 _lastVelocity;
    public Vector3 playerVelocity;
    private float   _playerSpeed;
    private Vector3 _moveInput;
    
    private CapsuleCollider _collider;
    
    private KeyCode _bulletTimeKey  = KeyCode.Q;
    private KeyCode _collectBallKey = KeyCode.E;
    
    private Vector3 _currentStartAngularVelocity;
    
    private ParticleSystem _ballHitParticles;
    
    private float _shootCooldownTimer;
    
    private PlayerBall _ballInHold;
    
    //Ropes
    private Rope _hookRope;
    private PlayerBall _hookPullingBall;
    private bool _hookPulled;
    private bool _pulledBall;
    private float _hookTimer;
    private float _hookFlyTimer;
    private Vector3 _hookTargetPoint;
    private GameObject _scopeObject;
    
    private Collider[] _imaginaryBallAreas;

    private Vector3 _spawnPosition;
    
    private LineRenderer     _ballPredictionLineRenderer;
    private PlayerBall       _playerBallPrefab;
    private List<PlayerBall> _balls = new();
    
    [Header("Debug")]
    [SerializeField] private bool showPlayerStats;
    
    private TextMeshProUGUI _speedTextMesh;
    
    private void Awake(){
        Application.targetFrameRate = 200;
        
        switch (playerClass){
            case PlayerClass.Attacker:
                _player = attackerSettings;
                break;
            case PlayerClass.Balanced:
                _player = balancedSettings;
                break;
        }
        
        _collider = GetComponent<CapsuleCollider>();
        
        _playerBallPrefab           = GetPrefab("PlayerBall").GetComponent<PlayerBall>();
        _ropePrefab                 = GetPrefab("PlayerRope").GetComponent<Rope>();
        _scopeObject                = Instantiate(GetPrefab("ScopePrefab"));
        _ballPredictionLineRenderer = Instantiate(GetPrefab("PredictionTrail")).GetComponent<LineRenderer>();
        
        _currentStamina  = _player.maxStamina;
        _currentSpeed    = _player.baseSpeed;
        _currentFriction = _player.friction;
        
        _staminaSlider       = GameObject.FindWithTag("StaminaSlider").GetComponent<Slider>();
        _ballCounterTextMesh = GameObject.FindWithTag("BallCounter").GetComponent<TextMeshProUGUI>();
        
        _kickModelParticle = GameObject.FindWithTag("KickLeg").GetComponent<ParticleSystem>();
        
        _imaginaryBallAreas = new Collider[10];
        
        _currentBallCount = _player.maxBallCount;
        _ballCounterTextMesh.text = _currentBallCount.ToString();
        
        _speedTextMesh = GameObject.FindWithTag("SpeedText").GetComponent<TextMeshProUGUI>();
        
        if (!showPlayerStats){
            _speedTextMesh.gameObject.SetActive(false);
        }
        
        PlayerBall[] ballsOnScene = FindObjectsOfType<PlayerBall>();
        
        for (int i = 0; i < ballsOnScene.Length; i++){
            PlayerBall ball = ballsOnScene[i];
            ball.sleeping = true;
            InitBall(ref ball);
            _balls.Add(ball);
        }
    }

    private void OnEnable()
    {
        SceneLoader.BallSpawnerFound += OnBallSpawnerFound;
    }
    
    private void OnDisable()
    {
        SceneLoader.BallSpawnerFound -= OnBallSpawnerFound;
    }

    private void Start(){
        _kickHitParticles  = Particles.Instance.GetParticles("KickHitParticles");
        _ballHitParticles  = Particles.Instance.GetParticles("BallHitParticles");
        
        _spawnPosition = transform.position;
    }
    
    private float _previousDelta;
    private float _unscaledDelta;
    
    private void Update(){
        if (GAME_DELTA_SCALE <= 0){
            DebugStuff();
        
            return;
        }
    
        float fullDelta = Time.deltaTime * GAME_DELTA_SCALE;
        _unscaledDelta = Time.unscaledDeltaTime * GAME_DELTA_SCALE;
        fullDelta += _previousDelta;
        _previousDelta = 0;
        
        if (fullDelta > MIN_FRAME_DELTA){
            float delta = MIN_FRAME_DELTA * Time.timeScale * GAME_DELTA_SCALE;
            while (fullDelta > MIN_FRAME_DELTA){
                UpdateAll(delta);
                fullDelta -= MIN_FRAME_DELTA;
                _unscaledDelta = 0;
            }
            _previousDelta = fullDelta;
        } else{
            UpdateAll(fullDelta);
        }
    }
    
    private void UpdateAll(float delta){
        //var delta = delta * _playerTimeScale;
    
        _moveInput = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        
        var wishDirection = new Vector3(_moveInput.x, 0, _moveInput.z);
        wishDirection = directionTransform.right * wishDirection.x + directionTransform.forward * wishDirection.z;
        wishDirection.Normalize();
        
        _playerSpeed = playerVelocity.magnitude;

        if (IsGrounded()){
            _timeSinceGrounded = 0;
        
            GroundMove(delta, wishDirection);
            
            if (_jumpBufferTimer > 0){
                Jump(wishDirection);
            }
        } else{
            _timeSinceGrounded += delta;
        
            AirMove(delta, wishDirection);
        }
        
        var gravityMultiplierProgress = InverseLerp(0, _player.minJumpForce, playerVelocity.y);
        var gravityMultiplier         = Lerp(1, 2, gravityMultiplierProgress * gravityMultiplierProgress);
        playerVelocity += Vector3.down * _player.gravity * gravityMultiplier * delta;
        
        StaminaAbilities(delta, wishDirection);
        UpdateRopeMovement(delta);
                
        _playerSpeed = playerVelocity.magnitude;
        
        CalculatePlayerCollisions(ref playerVelocity, delta);
        
        _playerSpeed = playerVelocity.magnitude;
        
        transform.Translate(playerVelocity * delta);
        
        if (transform.position.y < -30){
            transform.position = _spawnPosition;
        }
        
        UpdateBalls(delta);
        
        if (!_holdingBall){
            _timeSinceHoldingBall += delta;
        } else{
            _timeSinceHoldingBall = 0;
        }
        
        UpdateKick(delta);
        BulletTime();
        
        if (showPlayerStats){
            var horizontalSpeed = (new Vector3(playerVelocity.x, 0, playerVelocity.z)).magnitude;
            _speedTextMesh.text = "Horizontal: " + horizontalSpeed;
        }
        
        if (!_holdingBall){
            //_playerTimeScale = Lerp(_playerTimeScale, 1, _unscaledDelta * 5);
        }
        
        DebugStuff();
    }
    
    private void UpdateRopeMovement(float delta){
        if (!Input.GetKey(KeyCode.Space)){
            return;
        }
        
        float ropeCheckRadius = 5f;
        float ropeDamping = 2f;
        float ropeTargetSpeed = 40;
        float ropeAcceleration = 120;
        bool foundOne = false;
        (Collider[], int) ropeColliders = CollidersInRadius(transform.position, ropeCheckRadius, Layers.Rope);
        for (int i = 0; i < ropeColliders.Item2; i++){
            if (ropeColliders.Item1[i].TryGetComponent<RopeNode>(out var ropeNode)){
                ropeNode.velocity.x += playerVelocity.x * delta * 10;
                ropeNode.velocity.z += playerVelocity.z * delta * 10;
                ropeNode.velocity.y -= playerVelocity.y * delta * 10;
                foundOne = true;
            }
        }
        if (foundOne){
            playerVelocity.x *= 1f - ropeDamping * delta;
            playerVelocity.z *= 1f - ropeDamping * delta;
            playerVelocity.y = MoveTowards(playerVelocity.y, ropeTargetSpeed, ropeAcceleration * delta);
        }
        if (CheckSphere(transform.position, ropeCheckRadius, Layers.Rope)){
        }
    }

    
    private void StaminaAbilities(float delta, Vector3 wishDirection){
        if (Input.GetKey(KeyCode.Space)){   
            if (_currentStamina > 0 && _jumpChargeProgress < 1){
                _jumpChargeProgress += _unscaledDelta / _player.timeToChargeMaxJump;
                _currentStamina -= _unscaledDelta * _player.jumpChargeStaminaDrain;
                //Debug.Log(_unscaledDelta);
                _jumpBufferTimer = 0;
                _currentFriction = _player.friction * 0.5f;
            } else{
                _currentFriction = _player.friction;
            }
        } 
        if (Input.GetKey(KeyCode.LeftShift)){
            if (_currentStamina > 0){
                _currentSpeed = _player.sprintSpeed;
                _currentStamina -= delta * _player.sprintStaminaDrain;
            } else {
                _currentSpeed = _player.baseSpeed;
            }
        }
        
        if (_jumpBufferTimer > 0){
            _jumpBufferTimer -= delta;
            if (_jumpBufferTimer <= 0){
                _currentStamina += _jumpChargeProgress * _player.timeToChargeMaxJump * _player.jumpChargeStaminaDrain;
                _jumpChargeProgress = 0;
            }
        }
        
        if (Input.GetKeyUp(KeyCode.Space)){
            if (IsGrounded() || _timeSinceGrounded <= _player.coyoteTime || _jumpBufferTimer > 0){
                Jump(wishDirection);
            } else{
                _jumpBufferTimer = _player.jumpBufferTime;
            }
            _currentFriction = _player.friction;
        }
        
        if (Input.GetKeyUp(KeyCode.LeftShift)){
            _currentSpeed = _player.baseSpeed;
        }
        
        if (!Input.GetKey(KeyCode.Space) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(_bulletTimeKey)){
            _currentStamina = Clamp(_currentStamina + _player.staminaRecoveryRate * delta, 0, _player.maxStamina);
        }
        
        //Rope logics
        if (Input.GetKeyDown(KeyCode.C)){
            Rope rope = Instantiate(_ropePrefab, BallStartPosition(), Quaternion.identity);
            rope.SetVelocityToFirstNode(GetCameraTransform().forward * _player.maxBallSpeed + playerVelocity * 0.5f);
        }
        if (_hookTimer <= 0){
            PlayerBall ballInHookRange = null;
            _hookTargetPoint = Vector3.zero;
            bool foundGroundPoint = false;
            if (SphereCast(GetCameraTransform().position, _player.ballDetectRadius, GetCameraTransform().forward, out var hit, 100, Layers.PlayerProjectile)){
                if (hit.transform.TryGetComponent<PlayerBall>(out var playerBall)){
                    ballInHookRange = playerBall;
                    _scopeObject.transform.position = hit.point;
                    _hookTargetPoint = hit.point;
                }
            } else if (Raycast(GetCameraTransform().position, GetCameraTransform().forward, out var hit1, 300, Layers.Environment)){
                _scopeObject.transform.position = hit1.point;
                _hookTargetPoint = hit1.point;
                foundGroundPoint = true;
            } else if (SphereCast(GetCameraTransform().position, _player.ballDetectRadius, GetCameraTransform().forward, out var hit2, 100, Layers.Environment)){
                _scopeObject.transform.position = hit2.point;
                _hookTargetPoint = hit2.point;
                foundGroundPoint = true;
            } else{
                //_hookTargetPoint = GetCameraTransform().position + GetCameraTransform().forward * 100;
                _scopeObject.transform.position = GetCameraTransform().position + GetCameraTransform().forward * 1000;
            }
            
            if (Input.GetKeyDown(KeyCode.V) && _hookTimer <= 0){
                if (foundGroundPoint || ballInHookRange){
                    //hookTargetPos = _hookTargetPoint;
                    _hookRope = Instantiate(_ropePrefab, transform.position - transform.forward, Quaternion.identity);
                    _hookRope.LockLastNode(GetCameraTransform(), transform.position - transform.forward);
                    _hookRope.SetVelocityToFirstNode(GetCameraTransform().forward * 20);
                    GameObject firstNodeObject = _hookRope.FirstNode().gameObject;
                    //Animations.Instance.MoveObject(ref firstNodeObject, _hookTargetPoint, _player.hookPullDelay, false, 0, (a) => Sqrt(a));
                }
                
                if (ballInHookRange){
                    _hookPullingBall = ballInHookRange;
                    _pulledBall = true;
                }
            }
        }
        
        if (_hookRope){
            if (_hookTimer <= _player.hookPullDelay){
                Vector3 targetPoint = _hookTargetPoint;
                if (_hookPullingBall){
                    targetPoint = _hookPullingBall.transform.position;
                    _scopeObject.transform.position = targetPoint;
                }
                var ropeTransform = _hookRope.FirstNode().transform;
                MoveToPosition(ref ropeTransform, ref _hookFlyTimer, _player.hookPullDelay, BallStartPosition(), targetPoint, false, (a) => Sqrt(a));
            } else{
                _scopeObject.transform.position = _hookRope.FirstNode().transform.position;
            }
            _hookTimer += delta;
            if (_hookTimer >= _player.hookPullDelay && !_hookPulled){
                _hookPulled = true;
                if (_hookPullingBall){
                    _ballInHold = _hookPullingBall;
                    _hookPullingBall = null;
                    _hookRope.LockFirstNode(_ballInHold.transform, _ballInHold.transform.position);
                } else{
                    playerVelocity = _hookRope.EndToStartDirection() * _player.hookPullPower;
                }
                playerVelocity.y = Clamp(playerVelocity.y, 0, 40) + _player.hookUpPower;
            }
            if (_hookTimer >= _player.hookPullDelay * 2){
                _hookPulled = false;
                _hookRope.DestroyRope(1);
                _hookRope = null;
                _hookTimer = 0;
                _hookFlyTimer = 0;
            }
        }
        
        _staminaSlider.value = _currentStamina / _player.maxStamina;

    }
    
    private void UpdateKick(float delta){
        if (_kickCooldownTimer > 0){
            _kickCooldownTimer -= delta;
        }
        if (_kickCooldownTimer > 0){
            return;
        }
    
        if (_kickPerformingTimer <= 0 && Input.GetMouseButtonDown(0)){
            _kickPerformingTimer = _player.kickDuration;
            _kickModelParticle.Emit(1);
        }
    
        if (_kickPerformingTimer > 0){
            _kickPerformingTimer -= delta;
        
            Collider[] targets = GetKickTargetsInRange();            
            
            for (int i = 0; i < targets.Length; i++){
                int targetHash = targets[i].transform.parent ? targets[i].transform.parent.GetHashCode() : targets[i].GetHashCode();
                if (_alreadyHitByKick.Contains(targetHash)){
                    continue;
                }
                _alreadyHitByKick.Add(targetHash);
                
                Particles.Instance.SpawnAndPlay(_kickHitParticles, targets[i].ClosestPoint(transform.position));
                
                if (targets[i].transform.GetComponent<WinGate>()){
                    Win(targets[i].ClosestPoint(transform.position));
                    return;
                }
                
                //Ball kick
                var ball = targets[i].GetComponentInParent<PlayerBall>();
                if (ball){
                    ball.groundBounceCount = 0;
                    ball.lifeTime = 0;
                    StopHoldingBall();
                    SetKickVelocityToBall(ref ball);
                    
                    TimeController.Instance.AddHitStop(0.05f);
                    PlayerCameraController.Instance.ShakeCameraBase(0.7f);
                    
                    Vector3 targetScale = Vector3.one * 0.5f;
                    targetScale.z *= ball.velocity.magnitude * 0.2f;
                    Animations.Instance.ChangeScale(ball.transform.gameObject, targetScale, 0.1f, true, 0.3f, EaseOutQuint);
                    
                    float colliderSizeProgress = Clamp01(_playerSpeed / 50);
                    ball.sphere.radius = Lerp(1, 3, colliderSizeProgress);
                    
                    break;
                }
                
                var enemy = targets[i].GetComponentInParent<Enemy>();
                if (enemy){
                    enemy.TakeKick(GetCameraTransform().forward * _player.kickPower);
                    PlayerCameraController.Instance.ShakeCameraBase(0.8f);
                    TimeController.Instance.AddHitStop(0.1f);
                }
                
                if (targets[i].TryGetComponent<RopeNode>(out var ropeNode)){
                    ropeNode.velocity += GetCameraTransform().forward * _player.kickPower;
                }
            }
            
            if (_kickPerformingTimer <= 0){
                _kickCooldownTimer = _player.kickCooldown;
                _alreadyHitByKick.Clear();
            }
        }
    
    }
    
    private Collider[] GetKickTargetsInRange(float mult = 1){
        var kickHitBoxCenter = transform.position + GetCameraTransform().forward * _player.kickHitBoxLength * 0.5f * mult;
        Collider[] targets = OverlapBox(kickHitBoxCenter, new Vector3(_player.kickHitBoxWidth * mult, _player.kickHitBoxWidth * mult, _player.kickHitBoxLength * mult) * 0.5f, GetCameraTransform().rotation, Layers.PlayerKickHitable);
        
        return targets;
    }
    
    private bool TargetInKickRange(){
        var targets = GetKickTargetsInRange();
        for (int i = 0; i < targets.Length; i++){
            var ball = targets[i].GetComponentInParent<PlayerBall>();
            if (ball){
                return true;
            }
        }
        return false;
    }
    
    private void Jump(Vector3 wishDirection){
        playerVelocity.y += Lerp(_player.minJumpForce, _player.maxJumpForce, _jumpChargeProgress);
        //Debug.Log(_jumpChargeProgress);
        playerVelocity += wishDirection * _player.jumpForwardBoost;
        
        PlayerCameraController.Instance.ShakeCameraLong(_jumpChargeProgress * 1f);
        
        _jumpBufferTimer = 0;
        _jumpChargeProgress = 0;
    }
    
    private void GroundMove(float delta, Vector3 wishDirection){
        var wishSpeed = wishDirection.sqrMagnitude * _currentSpeed;
        
        ApplyFriction(delta);
        
        var directionDotVelocity = Vector3.Dot(wishDirection, playerVelocity.normalized);
        var acceleration = directionDotVelocity < 0.5f ? _player.groundDeceleration : _player.groundAcceleration;
        
        Accelerate(delta, wishDirection, wishSpeed, acceleration);
        
        _playerSpeed = playerVelocity.magnitude;
        
        if (_playerSpeed > _player.baseSpeed && directionDotVelocity < 0.1f){
            PlayerCameraController.Instance.ShakeCameraRapid(delta * 5);
        }
        
        if (_playerSpeed <= EPSILON){
            playerVelocity = Vector3.zero;
        }
    }
    
    private void AirMove(float delta, Vector3 wishDirection){
        var wishSpeed = wishDirection.sqrMagnitude * _currentSpeed;
        
        var directionDotVelocity = Vector3.Dot(wishDirection, playerVelocity.normalized);
        var acceleration = directionDotVelocity < 0f ? _player.airDeceleration : _player.airAcceleration;

        Accelerate(delta, wishDirection, wishSpeed, acceleration);
    }
    
    private void Accelerate(float delta, Vector3 targetDirection, float wishSpeed, float acceleration){
        var speedInWishDirection = Vector3.Dot(playerVelocity, targetDirection);
        
        var speedDifference = wishSpeed - speedInWishDirection;        
        
        if (speedDifference <= 0){
            return;
        }
        
        var accelerationSpeed = acceleration * speedDifference * delta;
        if (accelerationSpeed > speedDifference){
            accelerationSpeed = speedDifference;
        }
        
        playerVelocity.x += targetDirection.x * accelerationSpeed;
        playerVelocity.z += targetDirection.z * accelerationSpeed;
    }
    
    private void ApplyFriction(float delta){
        Vector3 frictionForce = _currentFriction * -playerVelocity.normalized * delta;
        
        var playerSpeed = _playerSpeed;
        frictionForce = Vector3.ClampMagnitude(frictionForce, playerSpeed);
        
        playerVelocity += frictionForce;
    }  
    
    private void CalculatePlayerCollisions(ref Vector3 velocity, float delta){
        var sphereCenter1 = transform.position - Vector3.up * _collider.height * 0.5f;
        var sphereCenter2 = transform.position + Vector3.up * _collider.height * 0.5f;
        
        var deltaVelocity = velocity * delta;
        bool foundGround = false;
        
        (Collider[], int) groundColliders = CollidersInCapsule(sphereCenter1, sphereCenter2, _collider.radius, Layers.Environment);
        
        for (int i = 0; i < groundColliders.Item2; i++){
            Vector3 colPoint = groundColliders.Item1[i].ClosestPoint(transform.position);
            Vector3 vecToPlayer = (transform.position - colPoint);
            Vector3 dirToPlayer = vecToPlayer.normalized;
            
            if (Vector3.Dot(velocity, dirToPlayer) >= 0){
                continue;
            }
            
            if (Vector3.Angle(dirToPlayer, transform.up) <= 30){
                foundGround = true;
                if (!_grounded){
                    var landingSpeedProgress = -velocity.y / 75; 
                    PlayerCameraController.Instance.ShakeCameraLong(landingSpeedProgress);
                }
            }
            velocity -= dirToPlayer * Vector3.Dot(velocity, dirToPlayer);
            // Vector3 playerBottomPoint = transform.position - transform.up * _collider.height * 0.5f;
            // playerBottomPoint.y -= _collider.radius;
            // transform.position += (colPoint - playerBottomPoint);
        }
        
        _grounded = foundGround;
    }
    
    private void UpdateBalls(float delta){
        if (_currentBallCount < _player.maxBallCount){
            _ballReloadCountdown -= delta;
            if (_ballReloadCountdown <= 0){
                _currentBallCount++;
                _ballCounterTextMesh.text = _currentBallCount.ToString();
                
                if (_currentBallCount < _player.maxBallCount){
                    _ballReloadCountdown = _player.ballReloadTime;
                }
            }
        }
    
        if (_shootCooldownTimer > 0){
            _shootCooldownTimer -= delta;
        }
    
        var angularVelocityDecreaseMultiplier = 0.5f; 
        
        if (Input.GetMouseButtonDown(1)){
            _currentStartAngularVelocity = Vector3.zero;
        }
        
        HoldBallLogic(delta);
        
        if (Input.GetMouseButton(1)){
            PredictAndDrawBallTrajectory();
        } else{
            //_currentStartAngularVelocity = Vector3.Lerp(_currentStartAngularVelocity, Vector3.zero, delta * 4);
            angularVelocityDecreaseMultiplier = 2f;
        }
        if (Input.GetMouseButtonUp(1)){
            _ballPredictionLineRenderer.positionCount = 0;
            //StopHoldingBall(true);
        }
    
        if (Input.GetMouseButton(1) && Input.GetMouseButtonDown(0) && _shootCooldownTimer <= 0 && _currentBallCount > 0 && !TargetInKickRange() && !_ballInHold){
            PlayerBall newBall = SpawnPlayerBall();
            //newBall.index = _balls.Count;
            //_balls.Add(newBall);
            
            _currentBallCount--;
            _ballCounterTextMesh.text = _currentBallCount.ToString();
            if (_ballReloadCountdown <= 0){
                _ballReloadCountdown = _player.ballReloadTime;
            }
            
            _shootCooldownTimer = _player.shootCooldown;
        }
        
        _currentStartAngularVelocity = Vector3.Lerp(_currentStartAngularVelocity, Vector3.zero, _unscaledDelta * angularVelocityDecreaseMultiplier);
        
        
        for (int i = 0; i < _balls.Count; i++){
            if (_balls[i] == null || !_balls[i].gameObject.activeSelf){
                //_balls.RemoveAt(i);
                continue;
            }
            UpdateBall(_balls[i], delta);    
        }
    }
    
    private void UpdateBall(PlayerBall ball, float delta, bool imaginaryBall = false){
        if (ball.lifeTime < 1){
            ball.inHold = false;
        }
        
        if (ball.inHold){
            return;
        }
    
        ball.velocity += Vector3.down * _player.ballGravity * delta;
        
        ball.angularVelocity.y = Lerp(ball.angularVelocity.y, 0, delta * _player.angularVelocityDecreaseRate);
        ball.angularVelocity.x = Lerp(ball.angularVelocity.x, 0, delta * _player.angularVelocityDecreaseRate * 2);
        
        ball.velocityNormalized = ball.velocity.normalized;
        
        ball.velocityRight = Quaternion.Euler(0, 90, 0) * ball.velocityNormalized;
        ball.velocityUp    = Quaternion.AngleAxis(-90, ball.velocityRight)* ball.velocityNormalized;
        
        ball.speed = ball.velocity.magnitude;
        
        ball.velocity += (ball.velocityUp * ball.angularVelocity.x + ball.velocityRight * ball.angularVelocity.y) * _player.angularVelocityPower * delta;
        
        ball.transform.forward = ball.velocity;
        
        ball.lifeTime += delta;
        
        if (ball.groundBounceCount >= 1 
                && Raycast(ball.transform.position, ball.velocityNormalized, out var hit, 10, Layers.Environment) 
                && hit.normal.y == 1){
            ball.velocity *= 1f - delta * 5;
            /*
            if (ball.velocity.sqrMagnitude < 2 * 2){
                ball.velocity = Vector3.ClampMagnitude(ball.velocity, 1);
            }
            */
        }
    
        CalculateBallCollisions(ref ball, delta, imaginaryBall);
        
        ball.velocity.y = Clamp(ball.velocity.y, -150, 150);
        
        if (imaginaryBall){
            //We made it only for imaginary because otherwise WindGuy do it by himself
            //_enemiesController.CheckWindForImaginaryBall(ref ball, delta);
            
            Array.Clear(_imaginaryBallAreas, 0, _imaginaryBallAreas.Length);
            OverlapSphereNonAlloc(ball.transform.position, ball.sphere.radius, _imaginaryBallAreas, Layers.Area);
            
            for (int i = 0; i < _imaginaryBallAreas.Length; i++){
                if (_imaginaryBallAreas[i] == null){
                    continue;
                }
                
                if (_imaginaryBallAreas[i].TryGetComponent<WindArea>(out var wind)){
                    ball.velocity += wind.PowerVector(ball.transform.position) * delta;
                }
            }
            
            //Debug.Log(ball.transform.position);
        }
        
        if (!imaginaryBall){
            float ropeCheckRadius = 5f;
            (Collider[], int) ropesInRadius = CollidersInRadius(ball.transform.position, ropeCheckRadius, Layers.Rope);
            for (int i = 0; i < ropesInRadius.Item2; i++){
                if (ropesInRadius.Item1[i].TryGetComponent<RopeNode>(out var ropeNode)){
                    ropeNode.velocity += ball.velocity * delta * 20;
                }
            }
        }
    
        ball.transform.Translate(ball.velocity * delta, Space.World);
        
        if (imaginaryBall || (ball.lifeTime < 1 && !ball.hitEnemy)) return;
        
        /*
        if (!Input.GetKey(_collectBallKey)) return;
        //Ball collect logic
        var playerToBallVector = ball.transform.position - transform.position;
        if (playerToBallVector.sqrMagnitude < ballCollectRadius * ballCollectRadius){
            if (_currentBallCount < _player.maxBallCount){
                _currentBallCount++;
                _ballCounterTextMesh.text = _currentBallCount.ToString();
            }
            
            DisableBall(ref ball);
            //_balls.RemoveAt(ball.index);
            if (playerVelocity.y < 10){
                playerVelocity.y = 30;
            } else{
                playerVelocity.y += 20;
            }
        }
        */
    }
    
    private void ReflectToPlayer(ref PlayerBall ball, Enemy enemy){
        ball.velocity = (transform.position - enemy.transform.position).normalized * 10;
        ball.velocity.y = 20;
        //ball.angularVelocity.x = Clamp(ball.angularVelocity.x, 0, 15);
        ball.angularVelocity = Vector3.zero;
    }
    
    private void ReflectToNearbyEnemy(ref PlayerBall ball, Enemy enemy){
        Enemy closestEnemy = GetClosestEnemy(ball.transform.position, enemy.gameObject);
        if (closestEnemy){
            var ballToEnemyVectorNormalized = (closestEnemy.transform.position - ball.transform.position).normalized;
            ball.velocity = ballToEnemyVectorNormalized * 200;
        } else{
            ReflectToPlayer(ref ball, enemy);
        }
        ball.angularVelocity = Vector3.zero;
    }
    
    private bool BallRicocheCharged(ref PlayerBall ball){
        if (ball.ricocheCharged && ball.chargedBounceCount < 3){
            ball.chargedBounceCount++;    
            if (ball.chargedBounceCount >= 3){
                ball.ricocheCharged = false;
                ball.chargedBounceCount = 0;
                ball.chargedParticles.Stop();
            }
            
            return true;
        }
        
        return false;
    }
    
    private void CalculateBallCollisions(ref PlayerBall ball, float delta, bool imaginaryBall = false){
        //Layers. - gives us proper flag, but gameObject.layer gives us layer number from unity editor
        var deltaVelocity = ball.velocity * delta;
        
        var hitableLayers = Layers.PlayerBallHitable;
        /*
        if (imaginaryBall){
            hitableLayers &= ~(int)Layers.PlayerProjectile;
        }
        */
        RaycastHit[] enemyHits = SphereCastAll(ball.transform.position, ball.sphere.radius, ball.velocity.normalized, deltaVelocity.magnitude, Layers.EnemyHurtBox);

        for (int i = 0; i < enemyHits.Length; i++){
            if (enemyHits[i].transform == ball.transform) continue;
            
            if (!imaginaryBall && enemyHits[i].transform.GetComponent<WinGate>()){
                Win(enemyHits[i].point);
                return;
            }

            
            var enemy = enemyHits[i].collider.GetComponentInParent<Enemy>();
            
            if (!enemy || enemy.hitImmuneCountdown > 0 || Vector3.Dot(enemyHits[i].normal, ball.velocity) > 0) continue;

            if (ball.velocity.sqrMagnitude > 25){
                ball.bounceCount++;
                
                //if (ball.bounceCount > 20) continue;
                
                if (!imaginaryBall){
                    Particles.Instance.SpawnAndPlay(_ballHitParticles, enemyHits[i].point);
                }
            }

            bool hitBallLayer = ((1 << enemyHits[i].transform.gameObject.layer) & (int)Layers.PlayerProjectile) > 0;
            if (hitBallLayer){
            }

            if (enemy){
                if (!imaginaryBall){
                    if (enemy.effectsCooldown <= 0){
                        var hitStopMultiplier = 1.0f;
                        if (ball.ricocheCharged){
                            hitStopMultiplier = 3.0f;
                        }
                        
                        TimeController.Instance.AddHitStop(0.05f * hitStopMultiplier);
                        PlayerCameraController.Instance.ShakeCameraBase(0.3f);
                    }

                    switch (enemy.type){
                        case BlockerType:
                            enemy.TakeKick(ball.velocity);
                            break;
                        case WindGuyType:
                            enemy.TakeKick(ball.velocity);
                            break;
                        default:
                            enemy.TakeHit(enemyHits[i].collider);
                            break;
                    }
                
                    //ball.hitEnemy = true;
                } else{
                    GameObject enemyObject = enemy.gameObject;
                    Animations.Instance.ChangeMaterialColor(ref enemyObject, Colors.PredictionHitColor * 3, 0.02f);
                }

                switch (enemy.type){
                    case DummyType:
                        if (BallRicocheCharged(ref ball)){
                            ReflectToNearbyEnemy(ref ball, enemy);
                        } else{
                            ReflectToPlayer(ref ball, enemy);
                        }
                        break;
                    case HorizontalShooterType:
                    case VerticalShooterType:
                        if (BallRicocheCharged(ref ball)){
                            ReflectToNearbyEnemy(ref ball, enemy);
                        } else{
                            ReflectToPlayer(ref ball, enemy);
                        }
                        break;
                    case RicocheType:
                        ball.ricocheCharged = true;
                        ball.chargedParticles.Play();
                        ReflectToNearbyEnemy(ref ball, enemy);
                        break;
                    default:
                        if (BallRicocheCharged(ref ball)){
                            ReflectToNearbyEnemy(ref ball, enemy);
                        } else{
                            ReflectToPlayer(ref ball, enemy);
                        }
                        //ball.velocity = Vector3.Reflect(ball.velocity, enemyHits[i].normal) * 0.5f;
                        break;
                }
            }
        }
            
        RaycastHit[] otherHits = SphereCastAll(ball.transform.position, 0.5f, ball.velocity.normalized, deltaVelocity.magnitude, Layers.Environment | Layers.EnemyProjectile);
        
        for (int i = 0; i < otherHits.Length; i++){
            EnemyProjectile enemyProjectile = otherHits[i].transform.GetComponentInParent<EnemyProjectile>();
            if (!imaginaryBall && ball.speed > 35){
                Particles.Instance.SpawnAndPlay(_ballHitParticles, otherHits[i].point);
            }
            
            if (enemyProjectile){
                ball.velocity = Vector3.Reflect(ball.velocity, otherHits[i].normal);
                ball.velocity.y = 20;
                
                enemyProjectile.velocity = Vector3.Reflect(enemyProjectile.velocity, -otherHits[i].normal);
                continue;
            }
            
            Vector3 velocityReflectedVec = Vector3.Reflect(ball.velocity, otherHits[i].normal);
            float velocityMultiplier = 0.6f;
            if (velocityReflectedVec.y <= 5 && ball.speed <= 40){ 
                velocityMultiplier = 0.95f;
            }
            ball.velocity = velocityReflectedVec * velocityMultiplier;
            //ball.velocity = Vector3.ClampMagnitude(ball.velocity, 60);
            ball.angularVelocity = Vector3.zero;
        }
    }
    
    private void DisableBall(ref PlayerBall ball){
        ball.lifeTime = 0;
        ball.sphere.radius = 1;
        ball.bounceCount = 0;
        ball.groundBounceCount = 0;
        ball.hitEnemy = false;
        ball.inHold = false;
        ball.velocity = Vector3.zero;
        
        ball.gameObject.SetActive(false);
    }
    
    private PlayerBall BallInRange(float multiplier = 2){
        var kickTargets = GetKickTargetsInRange(multiplier);
        for (int i = 0; i < kickTargets.Length; i++){
            var ball = kickTargets[i].GetComponentInParent<PlayerBall>();
            if (ball && ball.lifeTime > 1){
                return ball;
            }
        }
        return null;
    }
    
    private void HoldBallLogic(float delta){
        //_holdingBall = false;
        
        // if (Input.GetMouseButtonDown(1)){
        //     //_ballInHold = BallInRange();
        // }
        
        float snappingBallTime = 0.3f;
        
        if (Input.GetMouseButton(1) && !_catchedBallOnInput){
            _ballInHold = BallInRange(4);
            if (_ballInHold){
                _ballInHold.velocity = playerVelocity;
                GameObject ballObject = _ballInHold.gameObject;
                //Animations.Instance.MoveObject(ref ballObject, BallStartPosition(), 0.1f, false, 0, (a) => a * a);
                Animations.Instance.ChangeMaterialColor(ref ballObject, Colors.BallHighlightColor, snappingBallTime);
                _catchedBallOnInput = true;
            }

            // if (!_ballInHold){
            //     _ballInHold = BallInRange();
            // }
            
            // if (_ballInHold){
            //     _holdingBallTime += delta;
            // }
        }
        
        if (Input.GetMouseButtonUp(1)){
            _catchedBallOnInput = false;
            //StopHoldingBall(true);
            //StopHoldingBall(true);
        }
        
        if (_ballInHold){
            float maxHoldTime = snappingBallTime;
            if (_pulledBall){
                maxHoldTime *= 4;
            }
            float ballSnapSpeed = 20;
            if (_pulledBall){
                ballSnapSpeed = 5;
            }
            
            _holdingBallTime += delta;
            _ballInHold.transform.position = Vector3.Lerp(_ballInHold.transform.position, BallStartPosition(), delta * ballSnapSpeed * Clamp(_playerSpeed / _player.baseSpeed, 1, 10));
            MoveSphereOutCollision(_ballInHold.transform, _ballInHold.sphere.radius, Layers.Environment);
            _ballInHold.velocity = playerVelocity;
            
            if (_holdingBallTime >= maxHoldTime){
                StopHoldingBall(true);
            }
        }
        /*
        return;

        if (!_ballInHold && (_timeSinceGrounded <= 0.3f || _timeSinceHoldingBall >= 0.5f)){
            var kickTargets = GetKickTargetsInRange(2f);
            for (int i = 0; i < kickTargets.Length; i++){
                var ball = kickTargets[i].GetComponentInParent<PlayerBall>();
                if (ball && ball.lifeTime > 1){
                    //if (_timeSinceGrounded <= 0.3f){
                    _ballInHold = ball;
                    //AirCatch
                    if (_timeSinceGrounded >= 0.3f){
                        GameObject ballObject = _ballInHold.gameObject;
                        //Animations.Instance.ChangeMaterialColor(ref ballObject, Colors.BallHighlightColor, 0.2f);
                    }
                    //} else{
                        //ball.velocity = playerVelocity;
                    //}
                    break;
                }
            }
        } 
        
        if (_ballInHold){
            _holdingBallTime += delta;
            if (_timeSinceGrounded <= 0.2f || _holdingBallTime <= 0.5f){
                GameObject ballObject = _ballInHold.gameObject;
                Animations.Instance.ChangeMaterialColor(ref ballObject, Colors.BallHighlightColor, 0.002f);
                _ballInHold.transform.position = Vector3.Lerp(_ballInHold.transform.position, BallStartPosition(), delta * 10 * Clamp(_playerSpeed / _player.baseSpeed, 1, 10));
                MoveSphereOutCollision(_ballInHold.transform, 0.5f, Layers.Environment);
                //_holdingBall = true;
                _ballInHold.inHold = true;
            } else{
                StopHoldingBall(true);
            }

        } else if (_ballInHold){
            StopHoldingBall(true);
        }
        */
    }
    
    private void PredictAndDrawBallTrajectory(){
        if (_player.haveScope){
            _currentStartAngularVelocity += new Vector3(Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), 0) * _player.angularVelocitySense;
            _currentStartAngularVelocity.x = Clamp(_currentStartAngularVelocity.x, -_player.maxAngularVelocity, _player.maxAngularVelocity);
            _currentStartAngularVelocity.y = Clamp(_currentStartAngularVelocity.y, -_player.maxAngularVelocity, _player.maxAngularVelocity);
        }
        
        if (_player.haveScope){
            var imaginaryBall = SpawnPlayerBall();
            imaginaryBall.imaginary = true;
            
            PlayerBall ballInKickRange = BallInRange(1);
            if (ballInKickRange){
                imaginaryBall.transform.position = ballInKickRange.transform.position;
                imaginaryBall.angularVelocity = ballInKickRange.angularVelocity;
                imaginaryBall.velocity = ballInKickRange.velocity;
            }
            
            SetKickVelocityToBall(ref imaginaryBall);
            //imaginaryBall.gameObject.name += "IMAGINE";
            
            var iterationCount = 200;
            var step = 0.02f;
            
            _ballPredictionLineRenderer.positionCount = iterationCount;
            for (int i = 0; i < iterationCount; i++){
                _ballPredictionLineRenderer.SetPosition(i, imaginaryBall.transform.position);
                UpdateBall(imaginaryBall, step, true);
            }
            
            DisableBall(ref imaginaryBall);
        }
    }
    
    private void StopHoldingBall(bool inheritVelocity = false){
        if (!_ballInHold) return;
    
        _ballInHold.inHold = false;
        _holdingBallTime = 0;
        _pulledBall = false;
        
        if (inheritVelocity) _ballInHold.velocity = playerVelocity;
        
        _ballInHold = null;
        //_holdingBall = false;
    }
    
    private PlayerBall SpawnPlayerBall(){
        PlayerBall newBall = null;
        for (int i = 0; i < _balls.Count; i++){
            if (!_balls[i].gameObject.activeSelf){
                newBall = _balls[i];
                newBall.gameObject.SetActive(true);
                newBall.transform.position = BallStartPosition();
                newBall.imaginary = false;
                newBall.chargedParticles.Stop();
                break;
            }
        }
        
        if (!newBall){
            newBall = Instantiate(_playerBallPrefab, BallStartPosition(), Quaternion.identity);
            InitBall(ref newBall);
            
            _balls.Add(newBall);
        }
        
        MoveSphereOutCollision(newBall.transform, 0.5f, Layers.Environment);
        
        return newBall;        
    }
    
    private void InitBall(ref PlayerBall ball){
        ball.index = _balls.Count;
        ball.sphere = ball.GetComponent<SphereCollider>();
    }
    
    private Vector3 BallStartPosition(){
        return GetCameraTransform().position + GetCameraTransform().forward * 2 - GetCameraTransform().up * 1.1f;
    }
    
    private void SetKickVelocityToBall(ref PlayerBall ball){
        ball.velocity = GetCameraTransform().forward * _player.maxBallSpeed + playerVelocity * 0.5f;
        ball.angularVelocity = _currentStartAngularVelocity;
    }
    
    private void OnBallSpawnerFound(Transform spawnPoint)
    {
        var ball = Instantiate(_playerBallPrefab, spawnPoint.position, spawnPoint.rotation);
        InitBall(ref ball);
            
        _balls.Add(ball);
    }
    
    private void BulletTime(){
        if (_currentStamina > 0 && Input.GetKey(_bulletTimeKey)){
            Time.timeScale = 0.05f;
            _currentStamina -= _unscaledDelta * _player.bulletTimeStaminaDrain;
        } 
        else if (Input.GetKeyUp(_bulletTimeKey) || _currentStamina <= 0){
            Time.timeScale = 1f;
        }
    }
    
    public void Win(Vector3 pos){
        TimeController.Instance.SlowToZero();
        for (int i = 0; i < 100; i++){
            Particles.Instance.SpawnAndPlay(_ballHitParticles, pos);
        }
    }
    
    public bool IsGrounded(){
        return _grounded;
    }
    
    public void ResetPosition(){
        transform.position = _spawnPosition;
    }
    
    public List<PlayerBall> GetBalls(){
        return _balls;
    }
    
    private void OnDrawGizmosSelected(){
        Gizmos.color = Color.blue;
        
        if (_player == null){
            _player = attackerSettings;
        }
        
        Gizmos.DrawWireSphere(transform.position, _player.ballCollectRadius);
        
        Gizmos.color = Color.red;
        
        
        var kickHitBoxCenter = transform.position + GetCameraTransform().forward * _player.kickHitBoxLength * 0.5f;
        Gizmos.DrawWireCube(kickHitBoxCenter, new Vector3(_player.kickHitBoxWidth, _player.kickHitBoxWidth, _player.kickHitBoxLength));
    }
    
    private void DebugStuff(){
        if (Input.GetKeyDown(KeyCode.L)){
            PlayerCameraController.Instance.ShakeCameraLong(1);
        }
        
        if (Input.GetKeyDown(KeyCode.K)){
            PlayerCameraController.Instance.ShakeCameraRapid(1);
        }
        
        if (Input.GetKeyDown(KeyCode.J)){
            PlayerCameraController.Instance.ShakeCameraBase(1);
        }
        
        if (Input.GetKeyDown(KeyCode.T)){
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        
        if (Input.GetKeyDown(KeyCode.P)){
            GAME_DELTA_SCALE = GAME_DELTA_SCALE < 1 ? 1 : 0;
        }
    }
}
