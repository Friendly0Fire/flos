using System.Collections.Generic;
using FLServer.DataWorkers;
using FLServer.Simulators;
using FLServer.Utils;

namespace FLServer.Object
{
    public abstract class SimObject : ReactorTimer, IUpdatable
    {

        /// <summary>
        /// Bucket of objects close to this SimObject - less than 1000M.
        /// TODO: use that
        /// </summary>
        public List<Tuple<double, SimObject>> Bucket = new List<Tuple<double, SimObject>>();

        /// <summary>
        /// Bucket of objects close to this SimObject - 10000M or in scanner range.
        /// TODO: use that
        /// </summary>
        public List<SimObject> ScanBucket = new List<SimObject>();

        protected uint BucketRange = 5000;

        private readonly bool _stationary;
        private bool _bucketInitialized;
        /// <summary>
        ///     The archetype id of the object.
        /// </summary>
        public Archetype Arch;

        /// <summary>
        ///     The health of this object, from 0 to 1.
        /// </summary>
        public float Health;

        /// <summary>
        ///     The ID of this object in space.
        /// </summary>
        public uint Objid;

        /// <summary>
        ///     The current orientation of this object.
        /// </summary>
        public Matrix Orientation = new Matrix();

        /// <summary>
        ///     The current position of this object
        /// </summary>
        public Vector Position = new Vector();

        /// <summary>
        ///     The controlling server
        /// </summary>
        public DPGameRunner Runner;

        /// <summary>
        ///     The current throttle position of this object.
        /// </summary>
        public float Throttle;

        /// <summary>
        ///     This is the time of the last position update to this object
        ///     since the object was created.
        /// </summary>
        public double UpdateTime;

        /// <summary>
        ///     Create the simulation object. Sometimes this object is used for data
        ///     storage rather than a live simulation object. This these cases,  the
        ///     runner can be null.
        /// </summary>
        /// <param name="runner"></param>
        protected SimObject(DPGameRunner runner)
        {
            Runner = runner;
            ExpireAfter(0.1);

            //TODO: check dis
             _stationary = (this is Solar.Solar);
        }

        /// <summary>
        ///     This method is called appromiately every 100ms. Inheriting classes should implement
        ///     movement in this.
        /// </summary>
        /// <param name="deltaSeconds">The time in seconds since the last update</param>
        public virtual bool Update(float deltaSeconds)
        {
            return true;
        }

        /// <summary>
        /// Updates bucket of near objects.
        /// </summary>
        private void UpdateBucket()
        {

            if (!_stationary)
            {
                Bucket.Clear();
                ScanBucket.Clear();
                foreach (var so in Runner.Objects)
                {
                    var pos = Position.DistanceTo(so.Value.Position);
                    if (pos < 1000) Bucket.Add(Tuple.New(pos, so.Value));
                    if (pos < BucketRange) ScanBucket.Add(so.Value);
                }
            }
            else
            {

                if (!_bucketInitialized)
                {
                    foreach (var so in Runner.Objects)
                    {
                        if (!(so.Value is Solar.Solar)) continue;
                        var pos = Position.DistanceTo(so.Value.Position);
                        if (pos < 1000) Bucket.Add(Tuple.New(pos, so.Value));
                        if (pos < BucketRange) ScanBucket.Add(so.Value);
                    }
                    _bucketInitialized = true;
                }

                Bucket.RemoveAll(v => !(v.Second is Solar.Solar));
                ScanBucket.RemoveAll(v => !(v is Solar.Solar));

                foreach (var so in Runner.AffObjects)
                {
                    var pos = Position.DistanceTo(so.Value.Position);
                    if (pos < 1000) Bucket.Add(Tuple.New(pos, so.Value));
                    if (pos < BucketRange) ScanBucket.Add(so.Value);
                }
            }

                
            
        }

        /// <summary>
        ///     Return the actual or estimated position of this simobject.
        /// </summary>
        /// <returns></returns>
        public abstract Vector ExtrapolatedPosition();

        /// <summary>
        /// 
        /// </summary>
        public override void HandleTimerEvent(double deltaSeconds)
        {
            
            if (Runner != null)
            {
                //TODO: CPU hog.
                UpdateBucket();
                //UpdateFarBucket();
            }


            if (Update((float)deltaSeconds))
                ExpireAfter(0.1);
        }


    }
}